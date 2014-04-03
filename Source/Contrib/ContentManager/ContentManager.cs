﻿// COPYRIGHT 2014 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ORTS.Settings;
using System.IO;

namespace ORTS.ContentManager
{
    public class ContentManager : Content
    {
        readonly FolderSettings Settings;

        public override ContentType Type { get { return ContentType.Root; } }

        public ContentManager(FolderSettings settings)
        {
            Settings = settings;
            Name = "Content Manager";
            PathName = "";
        }

        public override IEnumerable<Content> Get(ContentType type)
        {
            var content = new List<Content>();

            if (type == ContentType.Package)
            {
                // TODO: Support OR content folders.
                foreach (var folder in Settings.Folders)
                    content.Add(new ContentMSTSPackage(folder.Value));
            }
            //else if (type == ContentType.Route)
            //{
            //    foreach (var folder in Settings.Folders)
            //    {
            //        // TODO: Support OR content folders.
            //        var path = Path.Combine(folder.Value, "Routes");
            //        foreach (var route in Directory.GetDirectories(path))
            //            content.Add(new ContentMSTSRoute(Path.Combine(path, route)));
            //    }
            //}

            return content;
        }

        public override Content Get(string name, ContentType type)
        {
            if (type == ContentType.Package)
            {
                // TODO: Support OR content folders.
                if (Settings.Folders.ContainsKey(name))
                    return new ContentMSTSPackage(Settings.Folders[name]);
            }
            //else if (type == ContentType.Route)
            //{
            //    foreach (var folder in Settings.Folders)
            //    {
            //        // TODO: Support OR content folders.
            //        var path = Path.Combine(Path.Combine(folder.Value, "Routes"), name);
            //        if (File.Exists(path))
            //            return new ContentMSTSRoute(path);
            //    }
            //}

            return null;
        }
    }
}