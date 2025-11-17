using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Diplom_Bibl
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }




    private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            // Приводим sender к типу Button, чтобы получить доступ к её свойствам
            Button clickedButton = sender as Button;
            if (clickedButton != null)
            {
                // Получаем значение Tag кнопки
                string tag = clickedButton.Tag as string;

                // Проверяем, какая кнопка была нажата, и выполняем соответствующую навигацию
                switch (tag)
                {
                    case "Информационное обслуживание":
                        // Создаём экземпляр страницы
                        InformationServicePage informationServicePage = new InformationServicePage();
                        // Навигируем по Frame к этой странице
                        MainFrame.Navigate(informationServicePage);
                        break;
                    // Здесь можно добавить другие case для других кнопок, если они будут раскомментированы
                    // case "Читатели":
                    //     MainFrame.Navigate(new ReadersPage());
                    //     break;
                    // case "Библиотечный фонд":
                    //     MainFrame.Navigate(new LibraryFundPage());
                    //     break;
                    // case "Отчёты":
                    //     MainFrame.Navigate(new ReportsPage());
                    //     break;
                    default:
                        // Можно добавить логику для непредусмотренных случаев, если нужно
                        break;
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MainFrame.Navigate(new InformationServicePage());
        }
        // ... остальная часть класса
    }
}

