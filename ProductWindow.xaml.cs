using System;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Media;

namespace ChudoObuvApp
{
    public partial class ProductWindow : Window
    {
        private Tovar _product;
        private string _userLogin;
        private string _userRole;

        public ProductWindow(Tovar product, string login, string role)
        {
            InitializeComponent();
            _product = product;
            _userLogin = login;
            _userRole = role;

            // Заполняем данные
            TxtTitle.Text = _product.Name;
            TxtProizvodstvo.Text = "Производство: " + _product.Proizvodstvo;
            TxtCategoria.Text = "Категория: " + _product.Categoria;
            TxtDescription.Text = _product.Description;
            TxtPrice.Text = _product.PriceWithDiscountStr;
            ImgProduct.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(_product.PhotoPath));

            LoadSizes();
        }

        private void LoadSizes()
        {
            using (SqlConnection conn = Db.GetConnection())
            {
                conn.Open();
                string sql = @"
                    SELECT r.nazv_razmer 
                    FROM Sklad s
                    JOIN Razmeri r ON s.id_razmera = r.id_razmer
                    WHERE s.id_tovara = @id AND s.dostupnoe_kolichestvo > 0
                    ORDER BY r.nazv_razmer";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", _product.Id);
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    CmbSize.Items.Add(reader.GetString(0));
                }
            }

            if (CmbSize.Items.Count > 0)
                CmbSize.SelectedIndex = 0;
            else
            {
                TxtMessage.Text = "Нет в наличии.";
            }
        }

        private void BtnOrder_Click(object sender, RoutedEventArgs e)
        {
            // Проверка на гостя
            if (_userRole == "Неавторизованный пользователь")
            {
                TxtMessage.Text = "Войдите в систему, чтобы оформить заказ!";
                return;
            }

            if (CmbSize.SelectedItem == null)
            {
                TxtMessage.Text = "Выберите размер.";
                return;
            }

            int quantity;
            if (!int.TryParse(TxtQuantity.Text, out quantity) || quantity <= 0)
            {
                TxtMessage.Text = "Введите корректное количество (целое число больше 0).";
                return;
            }

            string size = CmbSize.SelectedItem.ToString();
            int available = GetAvailableQuantity(size);

            if (quantity > available)
            {
                TxtMessage.Text = $"На складе только {available} шт. этого размера.";
                return;
            }

            // Оформление заказа (запись в БД)
            try
            {
                using (SqlConnection conn = Db.GetConnection())
                {
                    conn.Open();

                    // 1. Создаем заказ (или берем последний)
                    string orderSql = "SELECT ISNULL(MAX(id_zakaza), 0) + 1 FROM Zakazi";
                    SqlCommand orderCmd = new SqlCommand(orderSql, conn);
                    int orderId = (int)orderCmd.ExecuteScalar();

                    string insertOrderSql = "INSERT INTO Zakazi (id_zakaza, data_zakaza, login) VALUES (@id, @date, @login)";
                    SqlCommand insertOrderCmd = new SqlCommand(insertOrderSql, conn);
                    insertOrderCmd.Parameters.AddWithValue("@id", orderId);
                    insertOrderCmd.Parameters.AddWithValue("@date", DateTime.Today);
                    insertOrderCmd.Parameters.AddWithValue("@login", _userLogin);
                    insertOrderCmd.ExecuteNonQuery();

                    // 2. Находим id размера
                    string sizeSql = "SELECT id_razmer FROM Razmeri WHERE nazv_razmer = @size";
                    SqlCommand sizeCmd = new SqlCommand(sizeSql, conn);
                    sizeCmd.Parameters.AddWithValue("@size", size);
                    int sizeId = (int)sizeCmd.ExecuteScalar();

                    // 3. Добавляем позицию заказа
                    string insertItemSql = @"
                        INSERT INTO Pozicii_zakazov (id_pozicii, id_zakaza, id_tovara, id_razmera, kolichestvo)
                        VALUES ((SELECT ISNULL(MAX(id_pozicii),0)+1 FROM Pozicii_zakazov), @orderId, @tovarId, @sizeId, @qty)";
                    SqlCommand insertItemCmd = new SqlCommand(insertItemSql, conn);
                    insertItemCmd.Parameters.AddWithValue("@orderId", orderId);
                    insertItemCmd.Parameters.AddWithValue("@tovarId", _product.Id);
                    insertItemCmd.Parameters.AddWithValue("@sizeId", sizeId);
                    insertItemCmd.Parameters.AddWithValue("@qty", quantity);
                    insertItemCmd.ExecuteNonQuery();

                    // 4. Обновляем склад
                    string updateStockSql = @"
                        UPDATE Sklad SET dostupnoe_kolichestvo = dostupnoe_kolichestvo - @qty
                        WHERE id_tovara = @tovarId AND id_razmera = @sizeId";
                    SqlCommand updateCmd = new SqlCommand(updateStockSql, conn);
                    updateCmd.Parameters.AddWithValue("@qty", quantity);
                    updateCmd.Parameters.AddWithValue("@tovarId", _product.Id);
                    updateCmd.Parameters.AddWithValue("@sizeId", sizeId);
                    updateCmd.ExecuteNonQuery();
                }

                MessageBox.Show("Заказ успешно оформлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                this.Close();
            }
            catch (Exception ex)
            {
                TxtMessage.Text = "Ошибка: " + ex.Message;
            }
        }

        private int GetAvailableQuantity(string size)
        {
            using (SqlConnection conn = Db.GetConnection())
            {
                conn.Open();
                string sql = @"
                    SELECT s.dostupnoe_kolichestvo 
                    FROM Sklad s
                    JOIN Razmeri r ON s.id_razmera = r.id_razmer
                    WHERE s.id_tovara = @id AND r.nazv_razmer = @size";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", _product.Id);
                cmd.Parameters.AddWithValue("@size", size);
                object result = cmd.ExecuteScalar();
                return result != null ? (int)result : 0;
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}