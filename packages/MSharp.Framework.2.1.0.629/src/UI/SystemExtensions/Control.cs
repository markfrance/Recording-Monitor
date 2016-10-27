namespace System
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Web.UI;
    using System.Web.UI.HtmlControls;
    using System.Web.UI.WebControls;

    partial class MSharpExtensions
    {
        /// <summary>
        /// Renders this control into a HTML string.
        /// </summary>
        public static string RenderHtml(this Control control)
        {
            var sb = new StringBuilder();
            using (var tw = new StringWriter(sb))
            {
                using (var writer = new HtmlTextWriter(tw))
                {
                    control.RenderControl(writer);
                    return sb.ToString();
                }
            }
        }
        public static void Add(this ControlCollection ctrl, string literal)
        {
            if (ctrl == null) throw new NullReferenceException();
            if (string.IsNullOrEmpty(literal)) return;

            ctrl.Add(new LiteralControl(literal));
        }

        public static void AddAt(this ControlCollection ctrl, int index, string literal)
        {
            if (ctrl == null) throw new NullReferenceException();
            if (string.IsNullOrEmpty(literal)) return;

            ctrl.AddAt(index, new LiteralControl(literal));
        }

        /// <summary>
        /// Hides all controls in a collection
        /// </summary>
        /// <param name="controls"></param>
        public static void HideAll(this ControlCollection controls)
        {
            foreach (Control c in controls)
            {
                try
                {
                    c.Visible = false;
                }
                catch (NotImplementedException)
                {
                    // Some controls, including the Extender controls, do not implement setting the Visible property.
                    // But this is not an issue since these controls have no visual representation at all.
                }
            }
        }

        /// <summary>
        /// Gets all parent controls of this control to the root.
        /// </summary>
        public static IEnumerable<Control> GetParentControls(this Control control)
        {
            if (control.Parent != null)
            {
                yield return control.Parent;
                foreach (var c in control.Parent.GetParentControls())
                    yield return c;
            }
        }

        /// <summary>
        /// Determines is this control is the parent or ancestor of another control.
        /// </summary>
        public static bool IsAncestorOf(this Control parent, Control another)
        {
            if (another == null) return false;
            if (another.Parent == parent) return true;
            return parent.IsAncestorOf(another.Parent);
        }

        /// <summary>
        /// Adds a specified control after another existing control in this collection.
        /// </summary>
        public static void AddAfter(this ControlCollection container, Control existingControl, Control newControl)
        {
            if (existingControl == null)
                throw new ArgumentNullException("existingControl");

            if (newControl == null)
                throw new ArgumentNullException("newControl");

            var index = container.IndexOf(existingControl);

            if (index == -1)
                throw new ArgumentException(existingControl + " (" + existingControl.ID + ") does is not a child of this controls container.");

            if (index == container.Count - 1) container.Add(newControl);
            else container.AddAt(index + 1, newControl);
        }

        /// <summary>
        /// Adds a specified html content after another existing control in this collection.
        /// </summary>
        public static void AddAfter(this ControlCollection container, Control existingControl, string content)
        {
            AddAfter(container, existingControl, new LiteralControl(content));
        }

        /// <summary>
        /// Adds a specified control before another existing control in this collection.
        /// </summary>
        public static void AddBefore(this ControlCollection container, Control existingControl, Control newControl)
        {
            if (existingControl == null)
                throw new ArgumentNullException("existingControl");

            if (newControl == null)
                throw new ArgumentNullException("newControl");

            var index = container.IndexOf(existingControl);

            if (index == -1)
                throw new ArgumentException(existingControl + " (" + existingControl.ID + ") does is not a child of this controls container.");

            container.AddAt(index + 1, newControl);
        }

        /// <summary>
        /// Adds a specified html content before another existing control in this collection.
        /// </summary>
        public static void AddBefore(this ControlCollection container, Control existingControl, string content)
        {
            AddBefore(container, existingControl, new LiteralControl(content));
        }

        /// <summary>
        /// Gets the first parent of this control which is of the specified type.
        /// </summary>
        public static T FindParent<T>(this Control control) where T : Control
        {
            var parent = control.Parent;
            while (parent != null)
            {
                if (parent is T)
                    return (T)parent;

                parent = parent.Parent;
            }

            return default(T);
        }

        /// <summary>
        /// Gets a Javascript expression that yield access to this control.
        /// </summary>        
        public static string ForBrowser(this Control control)
        {
            return "document.getElementById('" + control.ClientID + "')";
        }

        /// <summary>
        /// Gets a Javascript expression that yield a jQuery access to this control.
        /// </summary>        
        public static string ForJQuery(this Control control)
        {
            return "$(\"#{0}\")".FormatWith(control.ClientID);
        }

        /// <summary>
        /// Finds the parent of this control with the specified css class.
        /// It navigates the control tree up until it finds a matching control.
        /// </summary>
        public static Control FindParentWithCss(this Control control, string cssClass)
        {
            if (control == null)
                throw new NullReferenceException("Control.FindParent() extension method has been invoked on a null Control.");

            if (cssClass.IsEmpty())
                throw new ArgumentNullException("cssClass");

            for (var parent = control.Parent; parent != null; parent = parent.Parent)
            {
                var controlCssClass = parent.GetCssClass().Or("");

                if (controlCssClass.Split(' ').Trim().Contains(cssClass))
                    return parent;
            }

            return null;
        }

        /// <summary>
        /// Gets the Css class of this control.
        /// </summary>
        public static string GetCssClass(this Control control)
        {
            if (control == null)
                throw new NullReferenceException("Control.GetCssClass() extension method has been invoked on a null Control.");

            if (control is HtmlControl)
            {
                return (control as HtmlControl).Attributes["class"];
            }
            else if (control is WebControl)
            {
                return (control as WebControl).CssClass;
            }
            else
            {
                throw new NotSupportedException("GetCssClass() extension method does not support " + control.GetType());
            }
        }

        /// <summary>
        /// This solves the issue with multiple validation summary alert.
        /// </summary>
        public static void UnifyValidators(this Control control)
        {
            var validationSummaries = control.GetAllChildren().OfType<ValidationSummary>();
            var master = validationSummaries.First();
            validationSummaries.Skip(1).Do(a => a.Enabled = false);
            control.GetAllChildren().OfType<BaseValidator>().Do(a => a.ValidationGroup = master.ValidationGroup);
        }

        /// <summary>
        /// Gets all children of this control.
        /// </summary>
        public static IEnumerable<Control> GetAllChildren(this Control ctrl)
        {
            foreach (Control c in ctrl.Controls)
            {
                yield return c;

                foreach (var cc in c.GetAllChildren())
                    yield return cc;
            }
        }

        /// <summary>
        /// Transfers the children of this control to another specified control.
        /// </summary>
        public static void TransferChildrenTo(this Control oldContainer, Control newContainer)
        {
            if (oldContainer == null)
                throw new NullReferenceException();

            if (newContainer == null)
                throw new ArgumentNullException("newContainer");

            if (oldContainer == newContainer)
                throw new ArithmeticException("You cannot transfer the child controls of a control to itself.");

            var controls = oldContainer.Controls.Cast<Control>().ToArray();

            controls.Do(c =>
            {
                oldContainer.Controls.Remove(c);
                newContainer.Controls.Add(c);
            });
        }

        /// <summary>
        /// Transfers this control to a new parent.
        /// </summary>
        public static void TransferTo(this Control control, Control newParent)
        {
            if (newParent == null)
                throw new ArgumentNullException("newParent");

            if (control == newParent)
                throw new ArgumentException("A control cannot be transfered to itself.");

            //if (control.Parent == newParent)
            //return;

            control.Parent?.Controls.Remove(control);

            newParent.Controls.Add(control);
        }

        /// <summary>
        /// Iterator returns all validators for this control and optional Validation group.
        /// </summary>
        public static IEnumerable<BaseValidator> GetValidators(this Control control, string validationGroup = null)
        {
            return GetValidators<BaseValidator>(control, validationGroup);
        }

        /// <summary>
        /// Iterator returns all validators of type T for this control and optional Validation group.
        /// </summary>
        public static IEnumerable<T> GetValidators<T>(this Control control, string validationGroup = null) where T : BaseValidator
        {
            foreach (var validator in control.Page.Validators.OfType<T>())
            {
                if (validationGroup != null && validator.ValidationGroup != validationGroup) continue;

                if (validator.ControlToValidate != control.ID) continue;

                if (validator.NamingContainer != control.NamingContainer) continue;

                yield return validator;
            }
        }

        /// <summary>
        /// Returns the Control being validated by this Validator.
        /// </summary>
        public static T GetControlToValidate<T>(this BaseValidator validator) where T : Control
        {
            return validator.NamingContainer.FindControl(validator.ControlToValidate) as T;
        }

        /// <summary>
        /// Removes this Control from it's parent Control on this Page.
        /// </summary>
        public static void Remove(this Control control)
        {
            control.Parent?.Controls.Remove(control);
        }
    }
}
