//
// ThumbnailTagsCaptionRenderer.cs
//
// Author:
//   Mike Gemünde <mike@gemuende.de>
//
// Copyright (C) 2010 Novell, Inc.
// Copyright (C) 2010 Mike Gemünde
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using Gtk;
using Gdk;

using FSpot.Core;

namespace FSpot.Widgets
{
    public class ThumbnailTagsCaptionRenderer : ThumbnailCaptionRenderer
    {

#region private fields

        private int tag_icon_size;

        private int tag_icon_hspacing;

#endregion

#region Constructor

        public ThumbnailTagsCaptionRenderer () : this (16)
        {
        }

        public ThumbnailTagsCaptionRenderer (int tag_icon_size) : this (tag_icon_size, 2)
        {
        }

        public ThumbnailTagsCaptionRenderer (int tag_icon_size, int tag_icon_hspacing)
        {
            this.tag_icon_size = tag_icon_size;
            this.tag_icon_hspacing = tag_icon_hspacing;
        }

#endregion

#region Drawing Methods

        public override int GetHeight (Widget widget, int width)
        {
            return tag_icon_size;
        }

        public override void Render (Cairo.Context cr,
                                     Widget widget,
                                     Rectangle cell_area,
                                     Rectangle expose_area,
                                     StateType cell_state,
                                     IPhoto photo)
        {
            Tag [] tags = photo.Tags;
            Rectangle tag_bounds;

            tag_bounds.X = cell_area.X + (cell_area.Width  + tag_icon_hspacing - tags.Length * (tag_icon_size + tag_icon_hspacing)) / 2;
            tag_bounds.Y = cell_area.Y;
            tag_bounds.Width = tag_icon_size;
            tag_bounds.Height = tag_icon_size;


            foreach (Tag t in tags) {

                if (t == null)
                    continue;

                Pixbuf icon = t.Icon;

                Tag tag_iter = t.Category;
                while (icon == null && tag_iter != App.Instance.Database.Tags.RootCategory && tag_iter != null) {
                    icon = tag_iter.Icon;
                    tag_iter = tag_iter.Category;
                }

                if (icon == null)
                    continue;

                Rectangle region;
                if (tag_bounds.Intersect (expose_area, out region)) {
                    Pixbuf scaled_icon;
                    if (icon.Width == tag_bounds.Width) {
                        scaled_icon = icon;
                    } else {
                        scaled_icon = icon.ScaleSimple (tag_bounds.Width,
                                tag_bounds.Height,
                                InterpType.Bilinear);
                    }

                    Cms.Profile screen_profile;
                    if (FSpot.ColorManagement.Profiles.TryGetValue (Preferences.Get<string> (Preferences.COLOR_MANAGEMENT_DISPLAY_PROFILE), out screen_profile))
                        FSpot.ColorManagement.ApplyProfile (scaled_icon, screen_profile);

                    cr.Save ();
                    Gdk.CairoHelper.SetSourcePixbuf (cr, scaled_icon, tag_bounds.X, tag_bounds.Y);
                    cr.Rectangle (tag_bounds.X, tag_bounds.Y, tag_bounds.Width, tag_bounds.Height);
                    cr.Fill ();
                    cr.Restore ();

                    if (scaled_icon != icon) {
                        scaled_icon.Dispose ();
                    }
                }

                tag_bounds.X += tag_bounds.Width + tag_icon_hspacing;
            }
        }

#endregion

    }
}

