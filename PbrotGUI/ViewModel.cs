using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace PbrotGUI {
	class ViewModel : INotifyPropertyChanged {
		public ViewModel() {
		}

		private string _rendererString = "OpenMP";

		/// String property used in binding examples.
		public string RendererString {
			get {
				return _rendererString;
			}
			set {
				if(_rendererString != value) {
					_rendererString = value;
					NotifyPropertyChanged("RendererString");
				}
			}
		}

		private int _gridSize = 10000;

		public string GridSize {
			get {
				return _gridSize.ToString();
			}
			set {
				if(_gridSize.ToString() != value) {
					_gridSize = int.Parse(value);
					NotifyPropertyChanged("MemoryString");
				}
			}
		}

		public string MemoryString {
			get {
				return (_gridSize * _gridSize * 4) / (1024 * 1024) + " MB";
			}
		}

		#region INotifyPropertyChanged Members

		/// Need to implement this interface in order to get data binding
		/// to work properly.
		private void NotifyPropertyChanged(string propertyName) {
			if(PropertyChanged != null) {
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		#endregion
	}
}
