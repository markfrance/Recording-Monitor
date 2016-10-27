namespace MSharp.Framework
{
    using System;
    using System.Collections.Generic;
    using System.Xml.Linq;

    public class UndoContext
    {
        public readonly List<KeyValuePair<IApplicationEvent, string>> Operations = new List<KeyValuePair<IApplicationEvent, string>>();
        public DateTime Date { get; set; }

        /// <summary>
        /// Creates a new UndoContext instance.
        /// </summary>
        public UndoContext()
        {
            Date = LocalTime.Now;
        }

        public static UndoContext Deserialize(XElement change)
        {
            var result = new UndoContext
            {
                Date = DateTime.FromOADate(change.GetValue<double>("@Date"))
            };

            foreach (var op in change.Elements())
            {
                var eventId = op.GetValue<Guid>("@ID");
                var ev = Database.Get<IApplicationEvent>(eventId);
                result.Operations.Add(new KeyValuePair<IApplicationEvent, string>(ev, op.GetValue<string>("@Description")));
            }

            return result;
        }

        public string Serialize()
        {
            if (Operations.None()) return string.Empty;

            var xml = new XElement("Change", new XAttribute("Date", Date.ToOADate()));

            foreach (var item in Operations)
            {
                xml.Add(new XElement("Op", new XAttribute("ID", item.Key.GetId()), new XAttribute("Description", item.Value)));
            }

            return xml.ToString();
        }

        internal void Append(IApplicationEvent eventInfo, IEntity record)
        {
            string description;

            if (eventInfo.Event == "Delete")
                description = "Deleted {0} '{1}'".FormatWith(record.GetType().Name, record.ToString());
            else if (eventInfo.Event == "Insert")
                description = "Inserted {0} '{1}'".FormatWith(record.GetType().Name, record.ToString());
            else if (eventInfo.Event == "Update")
            {
                description = "Update {0} '{1}':".FormatWith(record.GetType().Name, record.ToString());

                if (eventInfo.Data.HasValue())
                {
                    foreach (var child in GetOldDataNode(eventInfo).Elements())
                    {
                        var property = child.Name.LocalName;

                        if (property == "IsCodeDirty") continue;

                        var oldValue = child.Value;
                        var newValue = EntityManager.ReadProperty(record, property).ToStringOrEmpty();

                        description += property + " changed from '{0}' to '{1}' | ".FormatWith(oldValue, newValue);
                    }
                }
            }
            else throw new NotSupportedException();

            Operations.Insert(0, new KeyValuePair<IApplicationEvent, string>(eventInfo, description.TrimEnd(" | ")));
        }

        public void Undo()
        {
            Database.EnlistOrCreateTransaction(() => { foreach (var op in Operations) Undo(op.Key); });
        }

        void Undo(IApplicationEvent operation)
        {
            Entity item;

            switch (operation.Event)
            {
                case "Insert":
                    Database.Delete(operation.LoadItem(), DeleteBehaviour.BypassAll);
                    break;
                case "Delete":
                    item = operation.LoadItem() as Entity;
                    Database.Save(item, SaveBehaviour.BypassSaved | SaveBehaviour.BypassSaving);
                    break;
                case "Update":
                    item = operation.LoadItem().Clone() as Entity;

                    foreach (var p in GetOldDataNode(operation).Elements())
                    {
                        var old = p.Value;
                        var property = item.GetType().GetProperty(p.Name.LocalName);
                        property.SetValue(item, old.To(property.PropertyType));
                    }

                    Database.Save(item, SaveBehaviour.BypassSaved | SaveBehaviour.BypassSaving);
                    break;
                default:
                    // Ignore other cases
                    break;
            }
        }

        static XElement GetOldDataNode(IApplicationEvent operation)
        {
            var node = XElement.Parse(operation.Data);
            return node.Element("old") ?? node;
        }
    }
}