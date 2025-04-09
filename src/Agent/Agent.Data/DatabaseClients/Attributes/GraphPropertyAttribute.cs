namespace Agent.Data.DatabaseClients.Attributes
{
    /// <summary>
    /// Attribute to mark a property as a graph property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class GraphPropertyAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the property in the graph database.
        /// </summary>
        public string PropertyName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphPropertyAttribute"/> class.
        /// </summary>
        /// <param name="propertyName">The name of the property in the graph database.</param>
        public GraphPropertyAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }
    }
}