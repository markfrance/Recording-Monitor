namespace AE.Net.Mail
{
    public class ImapClientExceptionEventArgs : System.EventArgs {
		public ImapClientExceptionEventArgs(System.Exception exception) {
			this.Exception = exception;
		}

		public System.Exception Exception { get; set; }
	}
}
