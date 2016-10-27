namespace System
{
    using System.Linq.Expressions;
    using System.Reflection;

    public class MethodPointer
    {
        #region Method
        /// <summary>
        /// Gets or sets the Method of this MethodPointer.
        /// </summary>
        public MethodInfo Method { get; set; }
        #endregion

        /// <summary>
        /// Gets the name of this method.
        /// </summary>
        public string Name { get { return Method.Name; } }

        /// <summary>
        /// Returns a string that represents this instance.
        /// </summary>
        public override string ToString()
        {
            return Name;
        }

        public static MethodPointer From(Expression<Func<object>> methodExpression)
        {
            var methodCallExpression = methodExpression.Body as MethodCallExpression;
            if (methodCallExpression != null)
                throw new Exception("The specified expression is " + methodExpression.Body.GetType().Name + ", not a MethodCallExpression: " + methodExpression);

            return new MethodPointer { Method = methodCallExpression.Method };
        }

        public static MethodPointer From(Expression<Action> methodExpression)
        {
            var methodCallExpression = methodExpression.Body as MethodCallExpression;
            if (methodCallExpression != null)
                throw new Exception("The specified expression is " + methodExpression.Body.GetType().Name + ", not a MethodCallExpression: " + methodExpression);

            return new MethodPointer { Method = methodCallExpression.Method };
        }

        public static MethodPointer From(Action method)
        {
            return new MethodPointer { Method = method.Method };
        }

        public static MethodPointer From<T>(Func<T> method)
        {
            return new MethodPointer { Method = method.Method };
        }      

        /// <summary>
        /// Performs an implicit conversion from a specified lambda expression to <see cref="System.MethodPointer"/>.
        /// </summary>
        public static implicit operator MethodPointer(Expression<Func<object>> methodExpression)
        {
            return From(methodExpression);
        }


        /// <summary>
        /// Performs an implicit conversion from a specified lambda expression to <see cref="System.MethodPointer"/>.
        /// </summary>
        public static implicit operator MethodPointer(Expression<Action> methodExpression)
        {
            return From(methodExpression);
        }

        /// <summary>
        /// Performs an implicit conversion from a specified lambda expression to <see cref="System.MethodPointer"/>.
        /// </summary>
        public static implicit operator MethodPointer(Action method)
        {
            return From(method);
        }

    }
}