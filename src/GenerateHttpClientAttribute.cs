namespace pefi.http
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class GenerateHttpClientAttribute : Attribute
    {
        public string OpenApiSpecUrl { get; }
        public string? ConfigurationName { get; set; }


        public GenerateHttpClientAttribute(string openApiSpecUrl)
        {
            OpenApiSpecUrl = openApiSpecUrl;
        }
    }
}
