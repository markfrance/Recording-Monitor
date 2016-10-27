namespace AE.Net.Mail.Imap
{
    public class Quota {
        private string Ressource;
        private string Usage;
        private int used;
        private int max;
        public Quota(string ressourceName, string usage, int used, int max) {
            this.Ressource = ressourceName;
            this.Usage = usage;
            this.used = used;
            this.max = max;
        }
        public virtual int Used {
            get { return this.used; }
        }
        public virtual int Max {
            get { return this.max; }
        }
    }
}