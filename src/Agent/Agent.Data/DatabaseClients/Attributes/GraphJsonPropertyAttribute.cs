namespace Agent.Data.DatabaseClients.Attributes
{
    /// <summary>
    /// Attribute to mark a property as a graph property as containing a Json string.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class GraphJsonPropertyAttribute : GraphPropertyAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GraphJsonPropertyAttribute"/> class.
        /// </summary>
        /// <param name="propertyName">The name of the property in the graph database.</param>
        public GraphJsonPropertyAttribute(string propertyName)
            : base(propertyName)
        {
        }
    }
}
