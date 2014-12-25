using System;
using System.Globalization;
using System.Windows.Data;

namespace PbrotGUI.WPFThings {
	public class OMPGridVisibilityConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			string strValue = value as String;
			return strValue.Equals("OpenMP") ? "Visible" : "Collapsed";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}
	}

	public class OCLGridVisibilityConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			string strValue = value as String;
			return strValue.Equals("OpenCL") ? "Visible" : "Collapsed";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}
	}

	public class TaskbarProgressModeConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			bool isIndeterminate = (bool)value;
			if(isIndeterminate)
				return "Indeterminate";
			return "Normal";
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}
	}

	public class StatusConverter : IValueConverter {
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
			// C# really needs to get its enum namespacing to work :/
			if((PbrotGUI.ViewModels.MainWindowViewModel.ProgramState)value == PbrotGUI.ViewModels.MainWindowViewModel.ProgramState.Idle
				|| (PbrotGUI.ViewModels.MainWindowViewModel.ProgramState)value == PbrotGUI.ViewModels.MainWindowViewModel.ProgramState.Finished)
				return true;
			return false;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
			throw new NotImplementedException();
		}
	}
}
