using System.Collections.Generic;

namespace MSharp.Framework.Services
{
    public interface IHierarchy : IEntity
    {
        IHierarchy GetParent();
        IEnumerable<IHierarchy> GetChildren();
        string Name { get; }
    }
}
