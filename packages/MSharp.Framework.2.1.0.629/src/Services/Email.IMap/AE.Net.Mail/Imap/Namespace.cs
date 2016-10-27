using System.Collections.ObjectModel;

namespace AE.Net.Mail.Imap
{
    public class Namespaces
    {
        private Collection<Namespace> _usernamespace = new Collection<Namespace>();
        private Collection<Namespace> _sharednamespace = new Collection<Namespace>();

        public virtual Collection<Namespace> ServerNamespace { get; } = new Collection<Namespace>();

        public virtual Collection<Namespace> UserNamespace
        {
            get { return this._usernamespace; }
        }
        public virtual Collection<Namespace> SharedNamespace
        {
            get { return this._sharednamespace; }
        }
    }

    public class Namespace
    {
        public Namespace(string prefix, string delimiter)
        {
            Prefix = prefix;
            Delimiter = delimiter;
        }
        public Namespace() { }
        public virtual string Prefix { get; internal set; }
        public virtual string Delimiter { get; internal set; }
    }
}