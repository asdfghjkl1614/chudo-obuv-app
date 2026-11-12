using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows;

namespace ChudoObuvApp
{
    public class OrderView
    {
        public int OrderId { get; set; }
        public DateTime Date { get; set; }
        public string ClientName { get; set; }
        public decimal TotalSum { get; set; }
    }

    public partial class OrdersWindow : Window
    {
        public OrdersWindow()
        {
            InitializeComponent();
            LoadOrders();
        }

        private void LoadOrders()
        {
            List<OrderView> orders = new List<OrderView>();
            using (SqlConnection conn = Db.GetConnection())
            {
                conn.Open();
                string sql = @"
                    SELECT z.id_zakaza, z.data_zakaza, p.fio AS ClientName, SUM(pz.kolichestvo * t.cena) AS TotalSum
                    FROM Zakazi z
                    JOIN Polzovateli p ON z.login = p.login
                    JOIN Pozicii_zakazov pz ON pz.id_zakaza = z.id_zakaza
                    JOIN Tovari t ON pz.id_tovara = t.id_tovara
                    GROUP BY z.id_zakaza, z.data_zakaza, p.fio
                    ORDER BY z.id_zakaza DESC";
                SqlCommand cmd = new SqlCommand(sql, conn);
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    orders.Add(new OrderView
                    {
                        OrderId = Convert.ToInt32(reader["id_zakaza"]),
                        Date = Convert.ToDateTime(reader["data_zakaza"]),
                        ClientName = reader["ClientName"].ToString(),
                        TotalSum = Convert.ToDecimal(reader["TotalSum"])
                    });
                }
            }
            DgOrders.ItemsSource = orders;
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) => this.Close();

        private void BtnDetails_Click(object sender, RoutedEventArgs e)
        {
            OrderView selected = DgOrders.SelectedItem as OrderView;
            if (selected == null) { MessageBox.Show("Выберите заказ."); return; }
            OrderDetailsWindow details = new OrderDetailsWindow(selected.OrderId);
            details.ShowDialog();
            LoadOrders();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            OrderView selected = DgOrders.SelectedItem as OrderView;
            if (selected == null) { MessageBox.Show("Выберите заказ для удаления."); return; }

            if (MessageBox.Show($"Удалить заказ №{selected.OrderId}?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using (SqlConnection conn = Db.GetConnection())
                {
                    conn.Open();
                    // Возврат на склад
                    string sql1 = @"UPDATE s SET s.dostupnoe_kolichestvo = s.dostupnoe_kolichestvo + pz.kolichestvo
                                    FROM Sklad s JOIN Pozicii_zakazov pz ON s.id_tovara = pz.id_tovara AND s.id_razmera = pz.id_razmera
                                    WHERE pz.id_zakaza = @id";
                    SqlCommand cmd1 = new SqlCommand(sql1, conn);
                    cmd1.Parameters.AddWithValue("@id", selected.OrderId);
                    cmd1.ExecuteNonQuery();

                    // Удаление позиций
                    SqlCommand cmd2 = new SqlCommand("DELETE FROM Pozicii_zakazov WHERE id_zakaza = @id", conn);
                    cmd2.Parameters.AddWithValue("@id", selected.OrderId);
                    cmd2.ExecuteNonQuery();

                    // Удаление заказа
                    SqlCommand cmd3 = new SqlCommand("DELETE FROM Zakazi WHERE id_zakaza = @id", conn);
                    cmd3.Parameters.AddWithValue("@id", selected.OrderId);
                    cmd3.ExecuteNonQuery();
                }
                LoadOrders();
            }
        }
    }
}