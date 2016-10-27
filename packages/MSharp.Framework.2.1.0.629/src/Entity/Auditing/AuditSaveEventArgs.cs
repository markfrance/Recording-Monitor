using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSharp.Framework.Data
{
    public class AuditSaveEventArgs : CancelEventArgs
    {
        public IEntity Entity { get; set; }
        public IApplicationEvent ApplicationEvent { get; set; }
        public SaveMode SaveMode { get; set; }
    }
}
