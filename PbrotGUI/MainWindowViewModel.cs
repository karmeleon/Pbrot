using PbrotGUI.Layouts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace PbrotGUI {
	class MainWindowViewModel : INotifyPropertyChanged {

		[DllImport("libpbrot.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern int GetString(StringBuilder str);


		[DllImport("libpbrot.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr GetCLDeviceStrings(out int stringCount);
		[DllImport("libpbrot.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr GetCLPlatformStrings(out int stringCount);
		[DllImport("libpbrot.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr GetCLMaxBufferSize(out int count);
		[DllImport("libpbrot.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr RunCLbrot(string kern, byte deviceNo, UInt32 gridSize, UInt32 maxIterations, UInt32 minIterations,
										UInt32 supersampling, float gridRange, float maxOrbit);

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

		public MainWindowViewModel() {
		}

		#region private fields

		private UInt32 _supersampling = 1;
		private float _maxOrbit = 8.0f;
		private UInt32 _gridSize = 10000;
		private UInt32 _maxIterations = 20;
		private UInt32 _minIterations = 0;
		private string _rendererString = "OpenMP";

		private UInt16 _OMPThreads = 1;

		private byte _selectedOCLDevice = 0;

		// holds the last image rendered
		private Bitmap _lastImage;
		// holds the WPF-friendly image
		private BitmapImage _bmpImage;
		// holds the raw 16-bit data of the image for saving full-quality .pngs
		//private Int16[] _rawImage;
		// holds the pointer to the last image rendered. must be freed manually
		//private IntPtr _lastImagePtr;

		#endregion

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

		public string GridSize {
			get {
				return _gridSize.ToString();
			}
			set {
				UInt32 temp;
				if(!UInt32.TryParse(value, out temp)) {
					throw new ApplicationException("Invalid resolution.");
				}
				if(_gridSize.ToString() != value) {
					_gridSize = temp;
					NotifyPropertyChanged("MemoryString");
				}
			}
		}

		public string Supersampling {
			get {
				return _supersampling.ToString();
			}
			set {
				if(!_supersampling.ToString().Equals(value)) {
					_supersampling = UInt32.Parse(value);
				}
			}
		}

		public string MaxOrbit {
			get {
				return _maxOrbit.ToString();
			}
			set {
				if(!_maxOrbit.ToString().Equals(value)) {
					_maxOrbit = UInt32.Parse(value);
				}
			}
		}

		public string MinIterations {
			get {
				return _minIterations.ToString();
			}
			set {
				UInt32 temp;
				if(!UInt32.TryParse(value, out temp)) {
					throw new ApplicationException("Invalid minimum iterations.");
				}
				if(_gridSize.ToString() != value) {
					_minIterations = temp;
				}
			}
		}

		public string MaxIterations {
			get {
				return _maxIterations.ToString();
			}
			set {
				UInt32 temp;
				if(!UInt32.TryParse(value, out temp)) {
					throw new ApplicationException("Invalid maximum iterations.");
				}
				if(_gridSize.ToString() != value) {
					_maxIterations = temp;
				}
			}
		}

		public string RendererSettingsString {
			get {
				return _rendererString + " settings";
			}
		}

		void RunBuddhabrot() {
			if(_rendererString.Equals("OpenCL")) {
				// read buddhabrot.cl from assembly
				string kernel;
				using(Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PbrotGUI.CL.buddhabrot.cl"))
				using(StreamReader reader = new StreamReader(stream)) {
					kernel = reader.ReadToEnd();
				}
				IntPtr CLbrotResult = RunCLbrot(kernel, _selectedOCLDevice, _gridSize, _maxIterations, _minIterations, _supersampling, 2.0f, _maxOrbit);
				_SaveImages(CLbrotResult);
				ImageViewerWindow newWin = new ImageViewerWindow();
				newWin.Show();
				Console.WriteLine("all done!");
			} else {
				// OMP goes here eventually
			}
		}

		bool CanRunBuddhabrot() {
			return true;
		}

		public ICommand GoButton { get { return new RelayCommand(RunBuddhabrot, CanRunBuddhabrot); } }

		#region OMP grid

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
					_OMPThreads = UInt16.Parse(value);
					NotifyPropertyChanged("MemoryString");
				}
			}
		}

		#endregion

		#region OCL grid

		private ObservableCollection<OCLDevice> _OCLDevices = new ObservableCollection<OCLDevice>(getCLDevices());

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
				_selectedOCLDevice = (byte)value;
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

		#region image processing

		private void _SaveImages(IntPtr data) {
			// Fuck System.Image.Bitmap. I will be forever haunted by "A generic error occurred in GDI+"

			// intptr is stupid and doesn't support unsigned datatypes :(
			//_rawImage = new Int16[_gridSize * _gridSize];
			//Marshal.Copy(data, _rawImage, 0, (int)(_gridSize * _gridSize));
			//Marshal.FreeCoTaskMem(data);

			//byte[] RGBimage = new byte[_gridSize * _gridSize * 3];
			//for(UInt64 i = 0; i < _gridSize * _gridSize; i++) {
			//	byte curr = (byte)(_rawImage[i] >> 8);	// take the 8 most significant bits of the 16 bit data
			//	for(byte j = 0; j < 3; j++) {
			//		RGBimage[j + 3 * i] = curr;
			//	}
			//}
			//MemoryStream stream = new MemoryStream(RGBimage);
			//_lastImage = Image.FromStream(stream);

			//UInt16[] rawImage = new UInt16[_gridSize * _gridSize];
			//// Image class accepts 16-bit greyscale yayyyy
			//for(UInt32 i = 0; i < _gridSize * _gridSize; i++) {
			//	rawImage[i] = temp[i] < 0 ? (UInt16)((UInt16)(temp[i] + Int16.MaxValue) + Int16.MaxValue) : (UInt16)temp[i];
			//}
			// convert UInt16[] to Image
			//if(_lastImage != null)
			//	_lastImage.Dispose();
			//_lastImage = new Bitmap((int)_gridSize, (int)_gridSize, System.Drawing.Imaging.PixelFormat.Format16bppGrayScale);
			//Rectangle writeWindow = new Rectangle(0, 0, (int)_gridSize, (int)_gridSize);
			//BitmapData bmpData = _lastImage.LockBits(writeWindow, ImageLockMode.ReadWrite, _lastImage.PixelFormat);
			//bmpData.Scan0 = data;
			//_lastImage.UnlockBits(bmpData);

			//// then save it as a png so it's useful at all
			//// goddamn the Bitmap class is annoying to use
			//MemoryStream ms = new MemoryStream();
			//ms.Position = 0;
			//System.Threading.Thread.Sleep(50);
			//_lastImage.Save(ms, ImageFormat.Png);
			//BitmapImage image = new BitmapImage();
			//image.BeginInit();
			//image.StreamSource = ms;
			//image.EndInit();
		}

		#endregion

		#region INotifyPropertyChanged Members

		private void NotifyPropertyChanged(string propertyName) {
			if(PropertyChanged != null) {
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		#endregion

		public void OnWindowClosing(object sender, CancelEventArgs e) {
			if(_lastImage != null)
				_lastImage.Dispose();
		}
	}
}
