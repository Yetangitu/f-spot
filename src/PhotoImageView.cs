
namespace FSpot {
	public class PhotoImageView : ImageView {
		public static double ZoomMultipler = 1.1;

		public delegate void PhotoChangedHandler (PhotoImageView view);
		public event PhotoChangedHandler PhotoChanged;
		
		protected BrowsablePointer item;
		
		public PhotoImageView (IPhotoCollection query)
		{
			loader = new FSpot.AsyncPixbufLoader ();
			loader.AreaUpdated += HandlePixbufAreaUpdated;
			loader.AreaPrepared += HandlePixbufPrepared;
			loader.Done += HandleDone;

			this.SizeAllocated += HandleSizeAllocated;
			this.KeyPressEvent += HandleKeyPressEvent;
			this.ScrollEvent += HandleScrollEvent;
			this.Destroyed += HandleDestroy;
			query.ItemChanged += HandleQueryItemChanged;
			this.item = new BrowsablePointer (query, -1);
			item.IndexChanged += PhotoIndexChanged;
		}

		public BrowsablePointer Item {
			get {
				return item;
			}
		}

		public Photo Photo {
			get {
				return (Photo)item.Current;
			}
		}

		private IPhotoCollection query;
		public IPhotoCollection Query {
			get {
				return (IPhotoCollection)item.Collection;
			}
#if false
			set {
				if (query != null) {
					query.Changed -= HandleQueryChanged;
					query.ItemChanged -= HandleQueryItemChanged;
				}

				query = value;
				query.Changed += HandleQueryChanged;
				query.ItemChanged += HandleQueryItemChanged;
			}
#endif
		}

		public Gdk.Pixbuf CompletePixbuf ()
		{
			loader.LoadToDone ();
			return this.Pixbuf;
		}

		public void Reload ()
		{
			if (!Item.IsValid)
				return;
			
			PhotoIndexChanged (Item, null);
			
		}
		/*
		private void HandleQueryChanged (IBrowsableCollection browsable)
		{
			if (query == browsable)
				Reload ();
		}
		*/

		public void HandleQueryItemChanged (IBrowsableCollection browsable, int item)
		{
			if (item == Item.Index)
				Reload ();
		}

		// Display.
		private void HandlePixbufAreaUpdated (object sender, Gdk.Rectangle area)
		{
			area = this.ImageCoordsToWindow (area);
			this.QueueDrawArea (area.X, area.Y, area.Width, area.Height);
		}
		
		private void HandlePixbufPrepared (object sender, System.EventArgs args)
		{
			Gdk.Pixbuf prev = this.Pixbuf;
			Gdk.Pixbuf next = loader.Pixbuf;

#if SPEED_COPY_DATA
			if (next != null && prev != null && next.Width == prev.Width && prev.Height == next.Height)
				prev.CopyArea (0, 0, next.Width, next.Height, next, 0, 0);
			else
				next.Fill (0x00000000);
#endif
#if true
			try {
				System.Uri uri = Photo.DefaultVersionUri;
				Gdk.Pixbuf thumb = new Gdk.Pixbuf (ThumbnailGenerator.ThumbnailPath (uri));
				if (thumb != null && next != null)
					thumb.Composite (next, 0, 0,
							 next.Width, next.Height,
							 0.0, 0.0,
							 next.Width/(double)thumb.Width, next.Height/(double)thumb.Height,
							 Gdk.InterpType.Bilinear, 0xff);
				
				if (thumb != null) {
					if (!ThumbnailGenerator.ThumbnailIsValid (thumb, uri))
						FSpot.ThumbnailGenerator.Default.Request (uri.LocalPath, 0, 256, 256);
					
					thumb.Dispose ();
				}
			} catch (System.Exception e) {
				System.Console.WriteLine (e.ToString ());
			}
#endif

			this.Pixbuf = next;
			if (prev != null)
				prev.Dispose ();

			this.ZoomFit ();
		}

		private void HandleDone (object sender, System.EventArgs args)
		{
			// FIXME the error hander here needs to provide proper information and we should
			// pass the state and the write exception in the args
			Gdk.Pixbuf prev = this.Pixbuf;
			if (loader.Pixbuf == null) {
				this.Pixbuf = new Gdk.Pixbuf (PixbufUtils.ErrorPixbuf, 0, 0, 
							      PixbufUtils.ErrorPixbuf.Width, 
								      PixbufUtils.ErrorPixbuf.Height);
				
			} else {
				this.Pixbuf = loader.Pixbuf;
			}
			this.ZoomFit ();

			if (prev != this.Pixbuf && prev != null)
				prev.Dispose ();
		}		
		
		private bool fit = true;
		public bool Fit {
			get {
				return fit;
			}
			set {
				fit = value;
				if (fit)
					ZoomFit ();
			}
		}


		public double Zoom {
			get {
				double x, y;
				this.GetZoom (out x, out y);
				return x;
			}
			
			set {
				this.Fit = false;
				this.SetZoom (value, value);
			}
		}
		
		private void HandleSizeAllocated (object sender, Gtk.SizeAllocatedArgs args)
		{
			if (fit)
				ZoomFit ();
		}	

		bool load_async = true;
		FSpot.AsyncPixbufLoader loader;
		FSpot.AsyncPixbufLoader next_loader;

		private void PhotoIndexChanged (BrowsablePointer item, IBrowsableItem old_item) 
		{
			// If it is just the position that changed fall out
			if (old_item != null && Photo != null && Photo.Id == ((Photo)old_item).Id)
				return;

			if (load_async) {
				try {
					loader.Load (Photo.DefaultVersionPath);
				} catch (System.Exception e) {
					// FIXME we should check the exception type and do something
					// like offer the user a chance to locate the moved file and
					// update the db entry, but for now just set the error pixbuf

					Gdk.Pixbuf old = this.Pixbuf;
					this.Pixbuf = new Gdk.Pixbuf (PixbufUtils.ErrorPixbuf, 0, 0, 
								      PixbufUtils.ErrorPixbuf.Width, 
								      PixbufUtils.ErrorPixbuf.Height);
					if (old != null)
						old.Dispose ();

					this.ZoomFit ();
				}
			} else {	
				Gdk.Pixbuf old = this.Pixbuf;
				this.Pixbuf = FSpot.PhotoLoader.Load ((IPhotoCollection)item.Collection, 
								      item.Index);
				if (old != null)
					old.Dispose ();
			}
			
			this.UnsetSelection ();

			if (PhotoChanged != null)
				PhotoChanged (this);
		}

		private void ZoomFit ()
		{
			Gdk.Pixbuf pixbuf = this.Pixbuf;
			Gtk.ScrolledWindow scrolled = this.Parent as Gtk.ScrolledWindow;
			
			//System.Console.WriteLine ("ZoomFit");

			if (pixbuf == null)
				return;

			int available_width = this.Allocation.Width;
			int available_height = this.Allocation.Height;
		
			double zoom_to_fit = ZoomUtils.FitToScale ((uint) available_width, 
								   (uint) available_height,
								   (uint) pixbuf.Width, 
								   (uint) pixbuf.Height, 
								   false);
			
			double image_zoom = zoom_to_fit;
			/*
			System.Console.WriteLine ("Zoom = {0}, {1}, {2}", image_zoom, 
						  available_width, 
						  available_height);
			*/

			if (scrolled != null)
				scrolled.SetPolicy (Gtk.PolicyType.Never, Gtk.PolicyType.Never);

			this.SetZoom (image_zoom, image_zoom);
			
			if (scrolled != null)
				scrolled.SetPolicy (Gtk.PolicyType.Automatic, Gtk.PolicyType.Automatic);
		}

		protected override void OnDestroyed ()
		{
			System.Console.WriteLine ("I'm feeling better");
			base.OnDestroyed ();
		}

		[GLib.ConnectBefore]
		private void HandleKeyPressEvent (object sender, Gtk.KeyPressEventArgs args)
		{
			// FIXME I really need to figure out why overriding is not working
			// for any of the default handlers.

			switch (args.Event.Key) {
			case Gdk.Key.Page_Up:
			case Gdk.Key.KP_Page_Up:
				this.Item.MovePrevious ();
				break;
			case Gdk.Key.Home:
				this.Item.Index = 0;
				break;
			case Gdk.Key.End:
				this.Item.Index = this.Query.Count - 1;
				break;
			case Gdk.Key.space:
			case Gdk.Key.Page_Down:
			case Gdk.Key.KP_Page_Down:
				this.Item.MoveNext ();
				break;
			case Gdk.Key.Key_0:
				this.Fit = true;
				break;
			case Gdk.Key.Key_1:
				this.Zoom =  1.0;
				break;
			case Gdk.Key.Key_2:
				this.Zoom = 2.0;
				break;
			case Gdk.Key.KP_Subtract:
			case Gdk.Key.minus:
				this.Zoom /= ZoomMultipler;
				break;
			case Gdk.Key.plus:
			case Gdk.Key.KP_Add:
				this.Zoom *= ZoomMultipler;
				break;
			default:
				args.RetVal = false;
				return;
			}
			args.RetVal = true;
			return;
		}
		
		[GLib.ConnectBefore]
		private void HandleScrollEvent (object sender, Gtk.ScrollEventArgs args)
		{
			//For right now we just disable fit mode and let the parent event handlers deal
			//with the real actions.
			this.Fit = false;
		}
		
		private void HandleDestroy (object sender, System.EventArgs args)
		{
			//loader.AreaUpdated -= HandlePixbufAreaUpdated;
			//loader.AreaPrepared -= HandlePixbufPrepared;
			loader.Dispose ();
		}

		protected override bool OnDestroyEvent (Gdk.Event evnt)
		{
			System.Console.WriteLine ("I'm feeling better");
			return base.OnDestroyEvent (evnt);
		}
	}
}
