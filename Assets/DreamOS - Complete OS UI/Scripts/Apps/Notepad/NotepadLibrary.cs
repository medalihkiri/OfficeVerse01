// --- NotepadLibrary.cs ---
using System.Collections.Generic;
using UnityEngine;

namespace Michsky.DreamOS
{
    [CreateAssetMenu(fileName = "New Note Library", menuName = "DreamOS/New Note Library")]
    public class NotepadLibrary : ScriptableObject
    {
        public List<NoteItem> notes = new List<NoteItem>();

        [System.Serializable]
        public class SharedUser // NEW: Helper class for shared user data
        {
            public string userId;
            public string username; // We'll need this for the UI
            public string permission;
        }

        [System.Serializable]
        public class NoteItem
        {
            public string _id;
            public string noteTitle = "Note Title";
            [TextArea] public string noteContent = "Note Description";

            // --- NEW & UPDATED FIELDS FOR SHARING ---
            public string ownerId; // ID of the user who created the note
            public bool isShared;
            public List<SharedUser> sharedWith = new List<SharedUser>();
            // --- END NEW FIELDS ---

            [HideInInspector] public bool isDeleted = false;
            [HideInInspector] public bool isCustom = true; // All backend notes are custom
            [HideInInspector] public bool isModContent = false;
            [HideInInspector] public bool modHelper = false;
        }
    }
}