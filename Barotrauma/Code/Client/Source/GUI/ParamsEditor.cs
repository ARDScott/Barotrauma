﻿using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Barotrauma
{
    class ParamsEditor
    {
        private static ParamsEditor _instance;
        public static ParamsEditor Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ParamsEditor();
                }
                return _instance;
            }
        }

        public GUIListBox EditorBox { get; private set; }
        /// <summary>
        /// Uses Linq queries. Don't use too frequently or reimplement.
        /// </summary>
        public IEnumerable<SerializableEntityEditor> FindEntityEditors() => EditorBox.Content.RectTransform.Children
            .Select(c => c.GUIComponent as SerializableEntityEditor)
            .Where(c => c != null);

        public GUIListBox CreateEditorBox(RectTransform rectT = null)
        {
            rectT = rectT ?? new RectTransform(new Vector2(0.25f, 1), GUI.Canvas) { MinSize = new Point(340, GameMain.GraphicsHeight) };
            EditorBox = new GUIListBox(rectT)
            {
                Spacing = 10
            };
            return EditorBox;
        }

        public void Clear()
        {
            EditorBox.ClearChildren();
        }

        public ParamsEditor(RectTransform rectT = null)
        {
            EditorBox = CreateEditorBox();
        }
    }
}
