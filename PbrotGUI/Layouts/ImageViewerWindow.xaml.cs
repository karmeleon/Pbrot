using PbrotGUI.ViewModels;
using System.Drawing;
using System.Windows;

namespace PbrotGUI.Layouts {
	/// <summary>
	/// Interaction logic for ImageViewerWindow.xaml
	/// </summary>
	public partial class ImageViewerWindow : Window {
		private ImageViewerViewModel vm;

		public ImageViewerWindow(Bitmap bmp) {
			vm = new ImageViewerViewModel(bmp);
			DataContext = vm;
			Closing += vm.OnWindowClosing;
			InitializeComponent();
		}
	}
}
