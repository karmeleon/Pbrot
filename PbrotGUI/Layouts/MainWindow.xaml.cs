using PbrotGUI.ViewModels;
using System.Windows;

namespace PbrotGUI
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private MainWindowViewModel vm = new MainWindowViewModel();

		public MainWindow()
		{
			DataContext = vm;
			//Closing += vm.OnWindowClosing;
			InitializeComponent();
		}
	}
}
