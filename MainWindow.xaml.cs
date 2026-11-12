using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ChudoObuvApp
{
    public partial class MainWindow : Window
    {
        public string userLogin { get; set; }
        public string userName { get; set; }
        public string userRole { get; set; }

        private List<Tovar> allProducts = new List<Tovar>();

        public MainWindow()
        {
            InitializeComponent();
            SetUserData("", "Гость", "Неавторизованный пользователь");
        }

        public void SetUserData(string login, string name, string role)
        {
            userLogin = login;
            userName = name;
            userRole = role;

            TxtCurrentUser.Text = userName + " (" + userRole + ")";

            if (userRole == "Неавторизованный пользователь")
                BtnAuth.Content = "Войти";
            else
                BtnAuth.Content = "Выйти";

            // Показываем кнопку "Заказы" только Менеджеру и Администратору
            if (userRole == "Менеджер" || userRole == "Администратор")
                BtnOrders.Visibility = Visibility.Visible;
            else
                BtnOrders.Visibility = Visibility.Collapsed;

            LoadTovari();
        }

        public void LoadTovari()
        {
            allProducts.Clear();
            DateTime today = DateTime.Now;
            DateTime lastMonthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
            DateTime lastMonthEnd = new DateTime(today.Year, today.Month, 1).AddDays(-1);

            using (SqlConnection conn = Db.GetConnection())
            {
                conn.Open();
                string sql = @"
                    SELECT 
                        t.id_tovara,
                        t.nazv_tovara,
                        t.photo,
                        t.cena,
                        t.opisanie,
                        ISNULL((SELECT TOP 1 p.nazv__proizvodstva 
                         FROM Tovar_proizvodstvo tp 
                         JOIN Proizvodstvo p ON tp.id_proizvodstva = p.id_proizvodstva 
                         WHERE tp.id_tovara = t.id_tovara), '') as Proizvodstvo,
                        ISNULL((SELECT TOP 1 c.nazv_categorii 
                         FROM Tovar_categoria tc 
                         JOIN Categorii c ON tc.id_categorii = c.id_categorii 
                         WHERE tc.id_tovara = t.id_tovara), '') as Categoria,
                        ISNULL((SELECT SUM(s.dostupnoe_kolichestvo) FROM Sklad s WHERE s.id_tovara = t.id_tovara), 0) as TotalQty,
                        ISNULL(STUFF((SELECT '; ' + r.nazv_razmer 
                               FROM Sklad s 
                               JOIN Razmeri r ON s.id_razmera = r.id_razmer 
                               WHERE s.id_tovara = t.id_tovara AND s.dostupnoe_kolichestvo > 0 
                               FOR XML PATH('')), 1, 2, ''), 'Нет в наличии') as Razmery
                    FROM Tovari t
                ";

                SqlCommand cmd = new SqlCommand(sql, conn);
                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    Tovar t = new Tovar();
                    t.Id = Convert.ToInt32(reader["id_tovara"]);
                    t.Name = reader["nazv_tovara"].ToString();
                    t.Photo = reader["photo"] != DBNull.Value ? reader["photo"].ToString() : "picture.png";
                    t.Price = Convert.ToDecimal(reader["cena"]);
                    t.Description = reader["opisanie"].ToString();
                    t.Proizvodstvo = reader["Proizvodstvo"].ToString();
                    t.Categoria = reader["Categoria"].ToString();
                    t.Qty = Convert.ToInt32(reader["TotalQty"]);
                    t.Razmery = reader["Razmery"].ToString();

                    allProducts.Add(t);
                }
                reader.Close();
            }

            foreach (var t in allProducts)
            {
                bool hasOrders = CheckOrdersLastMonth(t.Id, lastMonthStart, lastMonthEnd);

                t.PriceWithDiscount = hasOrders ? t.Price : t.Price * 0.75m;
                t.HasDiscount = !hasOrders;
                t.QtyText = t.Qty > 5 ? "много" : "мало";

                if (t.Qty <= 3)
                    t.CardColor = new SolidColorBrush(Colors.LightCoral);
                else
                    t.CardColor = new SolidColorBrush(Color.FromRgb(210, 246, 231));

                string photoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", t.Photo);
                if (!System.IO.File.Exists(photoPath))
                    photoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", "picture.png");
                t.PhotoPath = photoPath;
            }

            CmbCategory.Items.Clear();
            CmbCategory.Items.Add("Все категории");
            var categories = allProducts.Select(p => p.Categoria).Distinct().Where(c => c != "").ToList();
            foreach (var cat in categories)
            {
                CmbCategory.Items.Add(cat);
            }
            CmbCategory.SelectedIndex = 0;
            CmbSort.SelectedIndex = 0;

            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (allProducts == null || allProducts.Count == 0) return;

            string searchText = TxtSearch.Text.ToLower().Trim();
            string selectedCategory = CmbCategory.SelectedItem as string;
            int sortIndex = CmbSort.SelectedIndex;

            IEnumerable<Tovar> query = allProducts;

            if (!string.IsNullOrEmpty(searchText))
            {
                query = query.Where(p => p.Name.ToLower().Contains(searchText) ||
                                         (p.Description != null && p.Description.ToLower().Contains(searchText)));
            }

            if (selectedCategory != null && selectedCategory != "Все категории")
            {
                query = query.Where(p => p.Categoria == selectedCategory);
            }

            if (sortIndex == 1)
                query = query.OrderBy(p => p.PriceWithDiscount);
            else if (sortIndex == 2)
                query = query.OrderByDescending(p => p.PriceWithDiscount);

            var result = query.ToList();

            ListTovari.ItemsSource = result;
            TxtCount.Text = "Показано: " + result.Count;
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void Border_Click(object sender, MouseButtonEventArgs e)
        {
            Border border = sender as Border;
            Tovar selectedTovar = border.DataContext as Tovar;

            if (selectedTovar != null)
            {
                ProductWindow pw = new ProductWindow(selectedTovar, userLogin, userRole);
                pw.ShowDialog();
                LoadTovari();
            }
        }

        private bool CheckOrdersLastMonth(int idTovara, DateTime start, DateTime end)
        {
            using (SqlConnection conn = Db.GetConnection())
            {
                conn.Open();
                string sql = @"
                    SELECT COUNT(*) 
                    FROM Pozicii_zakazov pz
                    JOIN Zakazi z ON pz.id_zakaza = z.id_zakaza
                    WHERE pz.id_tovara = @id AND z.data_zakaza >= @start AND z.data_zakaza <= @end";

                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", idTovara);
                    cmd.Parameters.AddWithValue("@start", start);
                    cmd.Parameters.AddWithValue("@end", end);
                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    return count > 0;
                }
            }
        }

        private void BtnAuth_Click(object sender, RoutedEventArgs e)
        {
            if (userRole == "Неавторизованный пользователь")
            {
                LoginWindow login = new LoginWindow();
                login.Owner = this;
                this.Hide();
                login.ShowDialog();
                this.Show();
            }
            else
            {
                MessageBoxResult result = MessageBox.Show("Вы точно хотите выйти?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    SetUserData("", "Гость", "Неавторизованный пользователь");
                }
            }
        }

        private void BtnOrders_Click(object sender, RoutedEventArgs e)
        {
            OrdersWindow ow = new OrdersWindow();
            ow.ShowDialog();
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadTovari();
        }
    }

    public class Tovar
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Photo { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; }
        public string Proizvodstvo { get; set; }
        public string Categoria { get; set; }
        public int Qty { get; set; }
        public string Razmery { get; set; }

        public decimal PriceWithDiscount { get; set; }
        public bool HasDiscount { get; set; }
        public string QtyText { get; set; }
        public Brush CardColor { get; set; }
        public string PhotoPath { get; set; }

        public string Nazv_tovara { get { return Name; } }
        public string ProizvodstvoStr { get { return "Производство: " + Proizvodstvo; } }
        public string CategoriaStr { get { return "Категория: " + Categoria; } }
        public string RazmeryStr { get { return "Размеры: " + Razmery; } }
        public string QtyStr { get { return "Количество: " + Qty + " (" + QtyText + ")"; } }
        public string PriceWithDiscountStr { get { return PriceWithDiscount.ToString("N0") + " ₽"; } }
        public string BasePrice { get { return HasDiscount ? Price.ToString("N0") + " ₽" : ""; } }
    }
}