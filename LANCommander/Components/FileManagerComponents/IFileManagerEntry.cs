﻿namespace LANCommander.Components.FileManagerComponents
{
    public interface IFileManagerEntry
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public long Size { get; set; }
        public FileManagerDirectory Parent { get; set; }
        public DateTime ModifiedOn { get; set; }
        public DateTime CreatedOn { get; set; }
    }
}
