/*
 * Widgets.EditorPage.cs
 *
 * Author(s)
 * 	Ruben Vermeersch <ruben@savanne.be>
 *
 * This is free software. See COPYING for details.
 */

/*
 * FIXME: add this back
 * if (! photo_view.GetSelection (out x, out y, out width, out height)) {
 * string msg = Catalog.GetString ("No selection available");
 * string desc = Catalog.GetString ("This tool requires an active selection. Please select a region of the phot
 *
 * HigMessageDialog md = new HigMessageDialog ((Gtk.Window)this.Toplevel, DialogFlags.DestroyWithParent,
 *                                              Gtk.MessageType.Error, ButtonsType.Ok,
 *                                              msg,
 *                                             desc);
 *
 * md.Run ();
 * md.Destroy ();
 * return;
 * }
 */

using FSpot;
using FSpot.Editors;
using FSpot.Utils;

using Gtk;

using Mono.Addins;
using Mono.Unix;

using System;
using System.Collections.Generic;

namespace FSpot.Widgets {
	public class EditorPage : SidebarPage {
		internal bool InPhotoView;
		private readonly EditorPageWidget EditorPageWidget;

		public EditorPage () : base (new EditorPageWidget (),
									   Catalog.GetString ("Edit"),
									   "mode-image-edit") {
			// TODO: Somebody might need to change the icon to something more suitable.
			// FIXME: The icon isn't shown in the menu, are we missing a size?
			EditorPageWidget = SidebarWidget as EditorPageWidget;
			EditorPageWidget.Page = this;
		}

		protected override void AddedToSidebar () {
			Sidebar.SelectionChanged += delegate (IBrowsableCollection collection) { EditorPageWidget.ShowTools (); };
			Sidebar.ContextChanged += HandleContextChanged;
		}

		private void HandleContextChanged (object sender, EventArgs args)
		{
			InPhotoView = (Sidebar.Context == ViewContext.Edit);
			EditorPageWidget.ChangeButtonVisibility ();
		}
	}

	public class EditorPageWidget : ScrolledWindow {
		private VBox widgets;
		private VButtonBox buttons;
		private Widget active_editor;

		private List<Editor> editors;
		private Editor current_editor;

		// Used to make buttons insensitive when selecting multiple images.
		private Dictionary<Editor, Button> editor_buttons;

		private EditorPage page;
		internal EditorPage Page {
			get { return page; }
			set { page = value; ChangeButtonVisibility (); }
		}

		public EditorPageWidget () {
			editors = new List<Editor> ();
			AddinManager.AddExtensionNodeHandler ("/FSpot/Editors", OnExtensionChanged);

			ShowTools ();
		}

		private void OnExtensionChanged (object s, ExtensionNodeEventArgs args) {
			// FIXME: We do not do run-time removal of editors yet!
			if (args.Change == ExtensionChange.Add)
				editors.Add ((args.ExtensionNode as EditorNode).GetEditor ());
		}

		internal void ChangeButtonVisibility () {
			foreach (Editor editor in editors) {
				Button button;
				editor_buttons.TryGetValue (editor, out button);

				// Ugly add-remove thing to maintain ordering.
				// Simply changing Button.Visible doesn't work,
				// as ShowAll is called higher up in the stack :-(
				buttons.Remove (button);
				if (Page.InPhotoView || editor.CanHandleMultiple)
					buttons.Add (button);
			}
		}

		public void ShowTools () {
			// Remove any open editor, if present.
			if (current_editor != null) {
				active_editor.Hide ();
				Remove (active_editor);
				active_editor = null;
				current_editor.Restore ();
				current_editor = null;
			}

			// No need to build the widget twice.
			if (buttons != null) {
				buttons.Show ();
				return;
			}

			if (widgets == null) {
				widgets = new VBox (false, 0);
				Viewport widgets_port = new Viewport ();
				widgets_port.Add (widgets);
				Add (widgets_port);
				widgets_port.ShowAll ();
			}

			// Build the widget (first time we call this method).
			buttons = new VButtonBox ();
			buttons.BorderWidth = 5;
			buttons.Spacing = 5;
			buttons.LayoutStyle = ButtonBoxStyle.Start;

			editor_buttons = new Dictionary<Editor, Button> ();
			foreach (Editor editor in editors) {
				// Build sidebar button and add it to the sidebar.
				Editor current = editor;
				Button button = new Button (editor.Label);
				button.Image = new Image (GtkUtil.TryLoadIcon (FSpot.Global.IconTheme, editor.IconName, 22, (Gtk.IconLookupFlags)0));
				button.Clicked += delegate (object o, EventArgs e) { ChooseEditor (current); };
				buttons.Add (button);
				editor_buttons.Add (editor, button);
			}

			widgets.Add (buttons);
			buttons.ShowAll ();
		}

		private void ChooseEditor (Editor editor) {
			SetupEditor (editor);

			if (!editor.CanBeApplied || editor.HasSettings)
				ShowEditor (editor);
			else
				Apply (editor); // Instant apply
		}

		private void SetupEditor (Editor editor) {
			EditorState state = editor.CreateState ();

			EditorSelection selection = new EditorSelection ();
			PhotoImageView photo_view = MainWindow.Toplevel.PhotoView.View;

			if (Page.InPhotoView && photo_view != null) {
				if (photo_view.GetSelection (out selection.x, out selection.y,
							out selection.width, out selection.height))
					state.Selection = selection;
				else
					state.Selection = null;
				state.PhotoImageView = photo_view;
			} else {
				state.Selection = null;
				state.PhotoImageView = null;
			}
			state.Items = Page.Sidebar.Selection.Items;

			editor.Initialize (state);
		}

		private void Apply (Editor editor) {
			SetupEditor (editor);

			// TODO: Provide some user feedback about this.
			if (!editor.CanBeApplied)
				return;

			// TODO: Might need to do some nicer things for multiple selections (progress?)
			editor.Apply ();
			ShowTools ();
		}

		private void ShowEditor (Editor editor) {
			SetupEditor (editor);
			current_editor = editor;

			buttons.Hide ();

			// Top label
			VBox vbox = new VBox (false, 4);
			Label label = new Label ();
			label.Markup = String.Format("<b>{0}</b>", editor.Label);
			vbox.PackStart (label, true, false, 5);

			// Optional config widget
			Widget config = editor.ConfigurationWidget ();
			if (config != null) {
				vbox.Add (config);
			}

			// Apply / Cancel buttons
			HButtonBox tool_buttons = new HButtonBox ();
			tool_buttons.LayoutStyle = ButtonBoxStyle.End;
			tool_buttons.Spacing = 5;
			tool_buttons.BorderWidth = 5;
			tool_buttons.Homogeneous = false;

			Button cancel = new Button (Stock.Cancel);
			cancel.Clicked += HandleCancel;
			tool_buttons.Add (cancel);

			Button apply = new Button (editor.ApplyLabel);
			apply.Image = new Image (GtkUtil.TryLoadIcon (FSpot.Global.IconTheme, editor.IconName, 22, (Gtk.IconLookupFlags)0));
			apply.Clicked += delegate { Apply (editor); };
			tool_buttons.Add (apply);

			// Pack it all together
			vbox.Add (tool_buttons);
			active_editor = vbox;
			widgets.Add (active_editor);
			active_editor.ShowAll ();
		}

		void HandleCancel (object sender, System.EventArgs args) {
			ShowTools ();
		}
	}
}
