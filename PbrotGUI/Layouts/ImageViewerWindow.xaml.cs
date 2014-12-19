using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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

namespace PbrotGUI.Layouts {
	/// <summary>
	/// Interaction logic for ImageViewerWindow.xaml
	/// </summary>
	public partial class ImageViewerWindow : Window {
		private ImageViewerViewModel vm;

		public ImageViewerWindow(Bitmap bmp) {
			vm = new ImageViewerViewModel(bmp);
			DataContext = vm;
			InitializeComponent();
		}
	}
}
