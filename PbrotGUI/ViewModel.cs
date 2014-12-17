using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace PbrotGUI {
	class ViewModel : INotifyPropertyChanged {

		[DllImport("libpbrot.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern int GetString(StringBuilder str);


		[DllImport("libpbrot.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr GetCLDeviceStrings(out int stringCount);
		[DllImport("libpbrot.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr GetCLPlatformStrings(out int stringCount);
		[DllImport("libpbrot.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr GetCLMaxBufferSize(out int count);

		private static string getStringFromDLL() {
			StringBuilder str = new StringBuilder();
			GetString(str);
			return str.ToString();
		}

		private static List<OCLDevice> getCLDevices() {
			int stringCount = 0;
			IntPtr devices = GetCLDeviceStrings(out stringCount);
			IntPtr platforms = GetCLPlatformStrings(out stringCount);
			IntPtr bufferSizesPtr = GetCLMaxBufferSize(out stringCount);
			Int32[] bufferSizes = new Int32[stringCount];
			Marshal.Copy(bufferSizesPtr, bufferSizes, 0, stringCount);
			// int, long, and long int are all 32-bit integers
			// and i can't convert a signed int < 0 to an unsigned
			// i hate you so much microsoft
			List<OCLDevice> output = new List<OCLDevice>();
			for(int i = 0; i < stringCount; i++) {
				IntPtr devicePtr = Marshal.ReadIntPtr(devices, i * IntPtr.Size);
				IntPtr platformPtr = Marshal.ReadIntPtr(platforms, i * IntPtr.Size);


				string device = Marshal.PtrToStringAnsi(devicePtr);
				string platform = Marshal.PtrToStringAnsi(platformPtr);

				device = device.Trim();
				platform = platform.Trim();

				UInt32 size = bufferSizes[i] < 0 ? (UInt32)(bufferSizes[i] + Int32.MaxValue) + Int32.MaxValue : (UInt32)bufferSizes[i];

				output.Add(new OCLDevice {
					Name = platform + " " + device,
					Id = i,
					MaxBufferSize = size
				});

				// free the heap memory we allocated in the dll
				Marshal.FreeCoTaskMem(devicePtr);
				Marshal.FreeCoTaskMem(platformPtr);
			}
			return output;
		}

		public ViewModel() {
		}

		private string _rendererString = "OpenMP";

		public string RendererString {
			get {
				return _rendererString;
			}
			set {
				if(_rendererString != value) {
					_rendererString = value;
					NotifyPropertyChanged("RendererString");
					NotifyPropertyChanged("RendererSettingsString");
				}
			}
		}

		private UInt64 _gridSize = 10000;

		public string GridSize {
			get {
				return _gridSize.ToString();
			}
			set {
				if(_gridSize.ToString() != value) {
					_gridSize = UInt64.Parse(value);
					NotifyPropertyChanged("MemoryString");
				}
			}
		}

		public string RendererSettingsString {
			get {
				return _rendererString + " settings";
			}
		}

		#region OMP grid

		private UInt64 _OMPThreads = 1;

		public string MemoryString {
			get {
				// this tends to overflow when using 32-bit ints, so gridSize and OMPThreads have to be 64-bit
				// OMP uses doubles, which are 4B each
				return (_gridSize * _gridSize * 4 * _OMPThreads) / (1024 * 1024) + " MB";
			}
		}

		public string OMPThreads {
			get {
				return _OMPThreads.ToString();
			}
			set {
				if(_OMPThreads.ToString() != value) {
					_OMPThreads = UInt64.Parse(value);
					NotifyPropertyChanged("MemoryString");
				}
			}
		}

		#endregion

		#region OCL grid

		private ObservableCollection<OCLDevice> _OCLDevices = new ObservableCollection<OCLDevice>(getCLDevices());
		private int _selectedOCLDevice = 0;

		public ObservableCollection<OCLDevice> OCLDevices {
			get {
				//if(_selectedOCLDevice == null && _OCLDevices.Count != 0)
				//	_selectedOCLDevice = _OCLDevices[0];
				return _OCLDevices;
			}
		}

		public int SelectedOCLDevice {
			get {
				return _selectedOCLDevice;
			}
			set {
				_selectedOCLDevice = value;
				NotifyPropertyChanged("OCLMaxBufferString");
			}
		}

		public string OCLBufferString {
			get {
				// OCL uses floats, which are 2B each
				return (_gridSize * _gridSize * 2) / (1024 * 1024) + " MB";
			}
		}

		public string OCLMaxBufferString {
			get {
				return _OCLDevices[_selectedOCLDevice].MaxBufferSize / (1024 * 1024) + " MB";
			}
		}

		#endregion

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
