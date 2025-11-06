using System;

namespace CodeHero.Web.Models
{
    public class DocInfo
    {
        public string? RelativePath { get; set; }
        public string? Title { get; set; }
        public string[]? Tags { get; set; }
        public string? LastModified { get; set; }
        public string[]? Headings { get; set; }
        public string[]? Links { get; set; }
    }
}
