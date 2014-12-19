using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PbrotGUI {
	class ImageViewerViewModel : INotifyPropertyChanged {

		private Bitmap _bmp;

		public Bitmap bmp {
			get {
				return _bmp;
			}
			set {
				_bmp = value;
				NotifyPropertyChanged("Image");
			}
		}

		public ImageViewerViewModel(Bitmap bmp) {
			this.bmp = bmp;
		}

		public BitmapImage Image {
			get {
				MemoryStream ms = new MemoryStream();
				bmp.Save(ms, ImageFormat.Bmp);
				ms.Position = 0;
				BitmapImage bmpImg = new BitmapImage();
				bmpImg.BeginInit();
				bmpImg.StreamSource = ms;
				bmpImg.CacheOption = BitmapCacheOption.OnLoad;
				bmpImg.EndInit();
				return bmpImg;
			}
		}

		#region Command handlers

		void SaveImage() {
			SaveFileDialog dlg = new SaveFileDialog();
			dlg.FileName = "buddhabrot";
			dlg.DefaultExt = ".png";
			dlg.Filter = "Images |*.png;*.jpg;*.jpeg;*.gif;*.bmp";

			Nullable<bool> result = dlg.ShowDialog();

			if(result == true) {
				bmp.Save(dlg.FileName);
			}
		}

		bool CanSaveImage() {
			return true;
		}

		public ICommand SaveAs { get { return new RelayCommand(SaveImage, CanSaveImage); } }

		#endregion

		#region INotifyPropertyChanged Members

		private void NotifyPropertyChanged(string propertyName) {
			if(PropertyChanged != null) {
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		#endregion
	}
}
