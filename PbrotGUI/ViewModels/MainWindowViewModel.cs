using PbrotGUI.Layouts;
using PbrotGUI.WPFThings;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace PbrotGUI.ViewModels {
	class MainWindowViewModel : INotifyPropertyChanged {

		[DllImport("libpbrot.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr GetCLDeviceStrings(out int stringCount);
		[DllImport("libpbrot.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr GetCLPlatformStrings(out int stringCount);
		[DllImport("libpbrot.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr GetCLMaxBufferSize(out int count);
		[DllImport("libpbrot.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern void RunCLbrot(string kern, byte deviceNo, UInt32 gridSize, UInt32 maxIterations, UInt32 minIterations,
										UInt32 supersampling, float gridRange, float maxOrbit);
		[DllImport("libpbrot.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr normalizeCLGrid();

		[DllImport("libpbrot.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern void RunOMPbrot(UInt16 numThreads, UInt32 gridSize, UInt32 maxIterations, UInt32 minIterations,
										UInt32 supersampling, byte unsafeMode, double gridRange, double maxOrbit, IntPtr progress);
		[DllImport("libpbrot.dll", CallingConvention = CallingConvention.Cdecl)]
		static extern IntPtr normalizeOMPGrid();

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
			//Marshal.FreeCoTaskMem(devices);
			//Marshal.FreeCoTaskMem(platforms);
			//Marshal.FreeCoTaskMem(bufferSizesPtr);
			return output;
		}

		public MainWindowViewModel(Grid ParentGrid) {
			_parentGrid = ParentGrid;
			worker.DoWork += aSyncDoWork;
			worker.RunWorkerCompleted += ASyncWorkerCompleted;
		}

		#region private fields

		public enum ProgramState {
			Idle, Calculating, Normalizing, Finished
		}

		private ProgramState _state = ProgramState.Idle;

		private UInt32 _supersampling = 1;
		private float _maxOrbit = 8.0f;
		private UInt32 _gridSize = 10000;
		private UInt32 _maxIterations = 20;
		private UInt32 _minIterations = 0;
		private string _rendererString = "OpenMP";

		private UInt16 _OMPThreads;
		private bool _unsafeMode = false;

		private byte _selectedOCLDevice = 0;

		private Bitmap _lastImage;

		private readonly BackgroundWorker worker = new BackgroundWorker();

		private IntPtr _OMPprogressPointer;
		private DispatcherTimer _progressTimer;
		private double _progressBarValue = 0;
		private Stopwatch _time;

		// validation with mvvm is really difficult :(
		private Grid _parentGrid;

		#endregion

		public ProgramState State {
			get {
				return _state;
			}
			set {
				_state = value;
				NotifyPropertyChanged("ProgressBarText");
				NotifyPropertyChanged("ProgressBarIndeterminate");
				NotifyPropertyChanged("ProgressBarValue");
				NotifyPropertyChanged("State");
			}
		}

		public bool HasErrors {
			get {
				return Validation.GetHasError(_parentGrid);
				//return _hasErrors;
			}
		}

		public string RendererString {
			get {
				return _rendererString;
			}
			set {
				_rendererString = value;
				NotifyPropertyChanged("RendererString");
				NotifyPropertyChanged("RendererSettingsString");
			}
		}

		public string GridSize {
			get {
				return _gridSize.ToString();
			}
			set {
				_gridSize = UInt32.Parse(value);
				NotifyPropertyChanged("MemoryString");
				NotifyPropertyChanged("OCLBufferString");
			}
		}

		public string Supersampling {
			get {
				return _supersampling.ToString();
			}
			set {
				_supersampling = UInt32.Parse(value);
			}
		}

		public string MaxOrbit {
			get {
				return _maxOrbit.ToString();
			}
			set {
				_maxOrbit = UInt32.Parse(value);
			}
		}

		public string MinIterations {
			get {
				return _minIterations.ToString();
			}
			set {
				_minIterations = UInt32.Parse(value);
			}
		}

		public string MaxIterations {
			get {
				return _maxIterations.ToString();
			}
			set {
				_maxIterations = UInt32.Parse(value);
			}
		}

		public string RendererSettingsString {
			get {
				return _rendererString + " settings";
			}
		}

		#region Running

		private void aSyncDoWork(object sender, DoWorkEventArgs e) {
			IntPtr result;
			_time = new Stopwatch();
			if(_rendererString.Equals("OpenCL")) {
				// read buddhabrot.cl from assembly
				string kernel;
				using(Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("PbrotGUI.CL.buddhabrot.cl"))
				using(StreamReader reader = new StreamReader(stream)) {
					kernel = reader.ReadToEnd();
				}
				_time.Start();
				RunCLbrot(kernel, _selectedOCLDevice, _gridSize, _maxIterations, _minIterations, _supersampling, 2.0f, _maxOrbit);
				_time.Stop();
				State = ProgramState.Normalizing;
				result = normalizeCLGrid();
			} else {
				_OMPprogressPointer = Marshal.AllocCoTaskMem(sizeof(UInt32));
				_progressTimer.Start();
				_time.Start();
				byte isUnsafe = _unsafeMode ? (byte)1 : (byte)0;
				RunOMPbrot(_OMPThreads, _gridSize, _maxIterations, _minIterations, _supersampling, isUnsafe, 2.0, _maxOrbit, _OMPprogressPointer);
				_time.Stop();
				State = ProgramState.Normalizing;
				result = normalizeOMPGrid();
			}
			// turn the array into a C# bitmap object
			_lastImage = _SaveImages(result);
		}

		private void ASyncWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
			if(_progressTimer != null && _progressTimer.IsEnabled)
				_progressTimer.Stop();
			Marshal.FreeCoTaskMem(_OMPprogressPointer);
			ImageViewerWindow newWin = new ImageViewerWindow(_lastImage);
			//image.Dispose();
			newWin.Show();
			State = ProgramState.Finished;
		}

		private void progressTick(object sender, EventArgs e) {
			Int32 progress = Marshal.ReadInt32(_OMPprogressPointer);
			UInt32 actualProgress;
			if(progress < 0)
				actualProgress = (UInt32)(progress + Int32.MaxValue);
			else
				actualProgress = (UInt32)progress;
			ProgressBarValue = actualProgress;
		}

		void RunBuddhabrot() {
			if(State != ProgramState.Finished && State != ProgramState.Idle)
				return;
			State = ProgramState.Calculating;
			worker.RunWorkerAsync();
			if(_rendererString.Equals("OpenMP")) {
				_progressTimer = new DispatcherTimer();
				_progressTimer.Interval = TimeSpan.FromMilliseconds(16);
				_progressTimer.Tick += progressTick;
			} else {

			}
		}

		bool CanRunBuddhabrot() {
			return true;
		}

		public ICommand GoButton { get { return new RelayCommand(RunBuddhabrot, CanRunBuddhabrot); } }

		public bool ProgressBarIndeterminate {
			get {
				return State == ProgramState.Normalizing || (State == ProgramState.Calculating && _rendererString.Equals("OpenCL"));
			}
		}

		public double ProgressBarValue {
			get {
				return _progressBarValue / (_gridSize * _supersampling);
			}
			set {
				_progressBarValue = value;
				NotifyPropertyChanged("ProgressBarText");
				NotifyPropertyChanged("ProgressBarValue");
			}
		}

		public string ProgressBarText {
			get {
				string str = "";
				switch(State) {
					case ProgramState.Idle:
						str = "Ready.";
						break;
					case ProgramState.Calculating:
						if(_rendererString.Equals("OpenCL")) {
							str = "Progress unavalable for OpenCL";
						} else {
							str = _progressBarValue + " / " + (_gridSize * _supersampling) + " rows completed";
						}
						break;
					case ProgramState.Normalizing:
						str = "Generating image...";
						break;
					case ProgramState.Finished:
						str = "Calculated in " + _time.Elapsed.TotalSeconds.ToString("#.###") + " seconds.";
						break;
				}
				return str;
			}
		}

		#endregion running

		#region OMP grid

		public string OMPThreads {
			get {
				if(_OMPThreads == 0) {
					_OMPThreads = 0;
					//foreach(var item in new ManagementObjectSearcher("Select NumberOfCores from Win32_Processor").Get()) {
					//	_OMPThreads += UInt16.Parse(item["NumberOfCores"].ToString());
					//}
					_OMPThreads = (UInt16)Environment.ProcessorCount;
					NotifyPropertyChanged("MemoryString");
				}
				return _OMPThreads.ToString();
			}
			set {
				if(_OMPThreads.ToString() != value) {
					_OMPThreads = UInt16.Parse(value);
					NotifyPropertyChanged("MemoryString");
				}
			}
		}

		public bool UnsafeMode {
			get {
				return _unsafeMode;
			}
			set {
				_unsafeMode = value;
				NotifyPropertyChanged("MemoryString");
			}
		}

		public string MemoryString {
			get {
				// this tends to overflow when using 32-bit ints, so gridSize and OMPThreads have to be 64-bit
				// OMP uses 16-bit ints, which are 2 bytes
				if(!UnsafeMode)
					return (_gridSize * _gridSize * 2 * _OMPThreads) / (1024 * 1024) + " MB";
				return (_gridSize * _gridSize * 2) / (1024 * 1024) + " MB";
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
				// OCL uses uint32s, which are 4 bytes each
				return (_gridSize * _gridSize * 4) / (1024 * 1024) + " MB";
			}
		}

		public string OCLMaxBufferString {
			get {
				return _OCLDevices[_selectedOCLDevice].MaxBufferSize / (1024 * 1024) + " MB";
			}
		}

		#endregion

		#region image processing

		private Bitmap _SaveImages(IntPtr data) {
			Bitmap image = new Bitmap((int)_gridSize, (int)_gridSize, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

			byte[] buffer = new byte[_gridSize * _gridSize];
			Marshal.Copy(data, buffer, 0, (int)(_gridSize * _gridSize));
			Marshal.FreeCoTaskMem(data);

			Rectangle rect = new Rectangle(0, 0, image.Width, image.Height);
			BitmapData bmpData = image.LockBits(rect, ImageLockMode.ReadWrite, image.PixelFormat);

			IntPtr firstLine = bmpData.Scan0;

			int bytes = Math.Abs(bmpData.Stride) * image.Height;
			byte[] rgbValues = new byte[bytes];

			Marshal.Copy(firstLine, rgbValues, 0, bytes);

			for(int i = 0; i < rgbValues.Length / 3; i++) {
				rgbValues[3 * i] = buffer[i];
				rgbValues[3 * i + 1] = buffer[i];
				rgbValues[3 * i + 2] = buffer[i];
			}

			Marshal.Copy(rgbValues, 0, firstLine, bytes);

			image.UnlockBits(bmpData);

			return image;
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
