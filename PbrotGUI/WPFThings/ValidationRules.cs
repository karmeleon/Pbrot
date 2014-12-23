using System;
using System.Globalization;
using System.Windows.Controls;

namespace PbrotGUI.WPFThings {
	public class PositiveNonzeroIntegerRule : ValidationRule {

		public PositiveNonzeroIntegerRule() {
		}

		public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
			Int64 number = 0;
			
			try {
				// see if this is an int at all
				if(((string)value).Length > 0)
					number = Int64.Parse((string)value);
			} catch(Exception e) {
				return new ValidationResult(false, "Illegal characters or " + e.Message);
			}

			if(number > 0)
				return new ValidationResult(true, null);
			return new ValidationResult(false, "Number must be greater than 0.");
		}
	}

	public class PositiveIntegerRule : ValidationRule {

		public PositiveIntegerRule() {
		}

		public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
			Int64 number = 0;

			try {
				// see if this is an int at all
				if(((string)value).Length > 0)
					number = Int64.Parse((string)value);
			} catch(Exception e) {
				return new ValidationResult(false, "Illegal characters or " + e.Message);
			}

			if(number >= 0)
				return new ValidationResult(true, null);
			return new ValidationResult(false, "Number must be greater than or equal to 0.");
		}
	}

	public class PositiveNonzeroDoubleRule : ValidationRule {

		public PositiveNonzeroDoubleRule() {
		}

		public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
			double number = 0;

			try {
				// see if this is a double at all
				if(((string)value).Length > 0)
					number = double.Parse((string)value);
			} catch(Exception e) {
				return new ValidationResult(false, "Illegal characters or " + e.Message);
			}

			if(number >= 0)
				return new ValidationResult(true, null);
			return new ValidationResult(false, "Number must be greater than or equal to 0.");
		}
	}
}
