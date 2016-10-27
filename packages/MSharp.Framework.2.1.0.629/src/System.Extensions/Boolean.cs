namespace System
{
    partial class MSharpExtensions
    {

        public static string ToString(this bool? value, string format)
        {
            return ("{0:" + format + "}").FormatWith(value);
        }



        /// <summary>
        /// Returns Yes or No string depending on whether the result is true of false.
        /// </summary>
        public static string ToYesNoString(this bool value, string yes = "Yes", string no = "No")
        {
            return value ? yes : no;
        }

        /// <summary>
        /// Returns Yes or No string depending on whether the result is true of false.
        /// </summary>
        public static string ToYesNoString(this bool? value, string yes = "Yes", string no = "No")
        {
            if (value == true) return yes;
            if (value == false) return no;

            return string.Empty;
        }

    }
}
