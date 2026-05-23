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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Diplom_Bibl.Pages
{
    /// <summary>
    /// Логика взаимодействия для Settings.xaml
    /// </summary>
    public partial class Settings : Page
    {
        public Settings()
        {
            InitializeComponent();
        }

        private void AddSpeciality_Click(object sender, RoutedEventArgs e)
        {
            Pages.AddSpeciality AddSpecialityPage = new Pages.AddSpeciality();
         
            this.NavigationService?.Navigate(AddSpecialityPage);
        }

        private void AddGroup_Click(object sender, RoutedEventArgs e)
        {
            Pages.AddGroup addGroupPage = new Pages.AddGroup();
            this.NavigationService?.Navigate(addGroupPage);
        }

        private void AddGener_Click(object sender, RoutedEventArgs e)
        {
            Pages.AddGener addGenerPage = new Pages.AddGener();
            this.NavigationService?.Navigate(addGenerPage);
        }

        private void AddAuthor_Click(object sender, RoutedEventArgs e)
        {
            Pages.AddAuthor addAuthorPage = new Pages.AddAuthor();
            this.NavigationService?.Navigate(addAuthorPage);
        }

        private void AddPublishingHouse_Click(object sender, RoutedEventArgs e)
        {
            Pages.AddPublishingHouse addPublishingHousePage = new Pages.AddPublishingHouse();
            this.NavigationService?.Navigate(addPublishingHousePage);
        }

        private void AddStatusBook_Click(object sender, RoutedEventArgs e)
        {
            Pages.AddStatusBook addStatusBookPage = new Pages.AddStatusBook();
            this.NavigationService?.Navigate(addStatusBookPage);
        }

        private void AddViewBook_Click(object sender, RoutedEventArgs e)
        {
            Pages.AddViewBook addViewBookPage = new Pages.AddViewBook();
            this.NavigationService?.Navigate(addViewBookPage);
        }

        private void AddUser_Click(object sender, RoutedEventArgs e)
        {
            Pages.AddUser addUser = new Pages.AddUser();
            this.NavigationService?.Navigate(addUser);
        }
    }
}
