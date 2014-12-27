using PbrotGUI.ViewModels;
using System.Windows;

namespace PbrotGUI
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private MainWindowViewModel vm;

		public MainWindow()
		{
			vm = new MainWindowViewModel(MainGrid);
			DataContext = vm;
			//Closing += vm.OnWindowClosing;
			InitializeComponent();
		}
	}
}
