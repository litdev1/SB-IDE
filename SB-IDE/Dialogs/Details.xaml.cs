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

namespace SB_IDE.Dialogs
{
    /// <summary>
    /// Interaction logic for Details.xaml
    /// </summary>
    public partial class Details : Window
    {
        SBInterop sbInterop;
        dynamic programDetails;

        public Details(string key, SBInterop sbInterop)
        {
            this.sbInterop = sbInterop;

            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;

            textBoxID.Text = key;
            programDetails = sbInterop.GetDetails(textBoxID.Text);
            ShowDetails();
        }

        private void ShowDetails()
        {
            if (null == programDetails) return;

            string id = programDetails.Id;
            string title = programDetails.Title;
            string description = programDetails.Description;
            string category = programDetails.Category;
            double myRating = programDetails.MyRating;
            double rating = programDetails.Rating;
            int popularity = programDetails.Popularity;
            int numberOfRatings = programDetails.NumberOfRatings;

            buttonSetRating.IsEnabled = programDetails.MyRating < 0;
            myRating = myRating > 0 ? myRating : 3;

            textBoxID.Text = id;
            textBoxTitle.Text = title;
            textBoxDescription.Text = description;
            textBoxCategory.Text = category;
            textBoxPopularity.Text = popularity.ToString();
            textBoxNumberofRatings.Text = numberOfRatings.ToString();
            textBoxRating.Text = rating.ToString();
            textBoxMyRating.Text = myRating.ToString();

            starRating.Width = 24 * rating;
            starMyRating.Width = 24 * myRating;
        }

        private void buttonSetRating_Click(object sender, RoutedEventArgs e)
        {
            double rating = 3;
            if (double.TryParse(textBoxMyRating.Text, out rating))
            {
                rating = Math.Min(5, Math.Max(1, rating));
                programDetails = sbInterop.SetRating(textBoxID.Text, rating);
                ShowDetails();
            }
        }

        private void buttonReload_Click(object sender, RoutedEventArgs e)
        {
            programDetails = sbInterop.GetDetails(textBoxID.Text);
            ShowDetails();
        }

        private void Rectangle_MouseMove(object sender, MouseEventArgs e)
        {
            if (programDetails.MyRating > 0) return;
            int myRating = 1 + (int)e.MouseDevice.GetPosition((Rectangle)sender).X / 24;
            textBoxMyRating.Text = myRating.ToString();
            starMyRating.Width = 24 * myRating;
        }
    }
}
