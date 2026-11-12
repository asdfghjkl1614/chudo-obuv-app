using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows;

namespace ChudoObuvApp
{
    public class OrderItemView
    {
        public string ProductName { get; set; }
        public string Size { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public partial class OrderDetailsWindow : Window
    {
        private int _orderId;

        public OrderDetailsWindow(int orderId)
        {
            InitializeComponent();
            _orderId = orderId;
            TxtTitle.Text = "Состав заказа №" + orderId;
            LoadData();
        }

        private void LoadData()
        {
            List<OrderItemView> items = new List<OrderItemView>();

            using (SqlConnection conn = Db.GetConnection())
            {
                conn.Open();

                // Загружаем дату
                string dateSql = "SELECT data_zakaza FROM Zakazi WHERE id_zakaza = @id";
                SqlCommand dateCmd = new SqlCommand(dateSql, conn);
                dateCmd.Parameters.AddWithValue("@id", _orderId);
                TxtDate.Text = Convert.ToDateTime(dateCmd.ExecuteScalar()).ToString("dd.MM.yyyy");

                // Загружаем позиции
                string sql = @"
                    SELECT t.nazv_tovara, r.nazv_razmer, pz.kolichestvo, t.cena
                    FROM Pozicii_zakazov pz
                    JOIN Tovari t ON pz.id_tovara = t.id_tovara
                    JOIN Razmeri r ON pz.id_razmera = r.id_razmer
                    WHERE pz.id_zakaza = @id";

                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@id", _orderId);
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    items.Add(new OrderItemView
                    {
                        ProductName = reader["nazv_tovara"].ToString(),
                        Size = reader["nazv_razmer"].ToString(),
                        Quantity = Convert.ToInt32(reader["kolichestvo"]),
                        Price = Convert.ToDecimal(reader["cena"])
                    });
                }
            }

            DgItems.ItemsSource = items;
        }

        private void BtnSaveDate_Click(object sender, RoutedEventArgs e)
        {
            DateTime newDate;
            if (!DateTime.TryParseExact(TxtDate.Text, "dd.MM.yyyy", null, System.Globalization.DateTimeStyles.None, out newDate))
            {
                MessageBox.Show("Введите дату в формате ДД.ММ.ГГГГ", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (SqlConnection conn = Db.GetConnection())
            {
                conn.Open();
                string sql = "UPDATE Zakazi SET data_zakaza = @date WHERE id_zakaza = @id";
                SqlCommand cmd = new SqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@date", newDate);
                cmd.Parameters.AddWithValue("@id", _orderId);
                cmd.ExecuteNonQuery();
            }

            MessageBox.Show("Дата обновлена.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}