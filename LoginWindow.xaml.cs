using System.Data.SqlClient;
using System.Windows;

namespace ChudoObuvApp
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            LoadUsers();
        }

        private void LoadUsers()
        {
            using (SqlConnection conn = Db.GetConnection())
            {
                conn.Open();
                string sql = "SELECT login FROM Polzovateli";
                SqlCommand cmd = new SqlCommand(sql, conn);
                SqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    UserComboBox.Items.Add(reader.GetString(0));
                }
            }
            if (UserComboBox.Items.Count > 0) UserComboBox.SelectedIndex = 0;
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            if (UserComboBox.SelectedItem == null)
            {
                MessageText.Text = "Выберите пользователя.";
                return;
            }

            string login = UserComboBox.SelectedItem.ToString();

            try
            {
                using (SqlConnection conn = Db.GetConnection())
                {
                    conn.Open();
                    // Твои таблицы: Polzovateli, Polzovateli_roli, Roli
                    string sql = @"
                        SELECT p.fio, r.nazv_roli
                        FROM Polzovateli p
                        JOIN Polzovateli_roli pr ON pr.login = p.login
                        JOIN Roli r ON r.id_roli = pr.id_roli
                        WHERE p.login = @login";

                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@login", login);
                    SqlDataReader reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        string fio = reader.GetString(0);
                        string role = reader.GetString(1);

                        MainWindow main = this.Owner as MainWindow;
                        if (main != null)
                        {
                            main.SetUserData(login, fio, role);
                        }
                        this.Close();
                    }
                    else
                    {
                        MessageText.Text = "У пользователя нет роли.";
                    }
                }
            }
            catch (System.Exception ex)
            {
                MessageText.Text = "Ошибка: " + ex.Message;
            }
        }
    }
}