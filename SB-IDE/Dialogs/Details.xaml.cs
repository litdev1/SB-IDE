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
        string key;
        SBInterop sbInterop;
        dynamic programDetails;

        public Details(string key, SBInterop sbInterop)
        {
            this.key = key;
            this.sbInterop = sbInterop;

            InitializeComponent();

            FontSize = 12 + MainWindow.zoom;

            programDetails = sbInterop.GetDetails(key);
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

            textBoxID.Text = id;
            textBoxTitle.Text = title;
            textBoxDescription.Text = description;
            textBoxCategory.Text = category;
            textBoxPopularity.Text = popularity.ToString();
            textBoxNumberofRatings.Text = numberOfRatings.ToString();
            textBoxRating.Text = rating.ToString();
            textBoxMyRating.Text = myRating > 0 ? myRating.ToString() : "3";
        }

        private void buttonSetRating_Click(object sender, RoutedEventArgs e)
        {
            double rating = 3;
            if (double.TryParse(textBoxMyRating.Text, out rating))
            {
                rating = Math.Min(5, Math.Max(1, rating));
                programDetails = sbInterop.SetRating(key, rating);
                ShowDetails();
            }
        }
    }
}
