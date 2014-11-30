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

namespace VideoScreensaver
{
    /// <summary>
    /// Interaction logic for PropertiesControl.xaml
    /// </summary>
    public partial class PropertiesControl : UserControl
    {
        public PropertiesControl()
        {
            InitializeComponent();
        }

        public string   PhotoTitle { get; set; }
        public DateTime PhotoTimeTaken { get; set; }
        public string   PhotoFilePath { get; set; }

        public void Show()
        {
            Title.Text = PhotoTitle;
            Title.Visibility = System.Windows.Visibility.Visible;

            TimeTaken.Text = PhotoTimeTaken.ToLongDateString();
            TimeTaken.Visibility = System.Windows.Visibility.Visible;

            Path.Text = PhotoTitle;
            Path.Visibility = System.Windows.Visibility.Visible;

        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
        }

    }
}
