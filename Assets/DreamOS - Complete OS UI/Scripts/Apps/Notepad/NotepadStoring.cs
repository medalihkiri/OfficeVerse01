// --- NotepadStoring.cs ---
using UnityEngine;
using UnityEngine.Networking;
using System.Collections.Generic;
using System;

namespace Michsky.DreamOS
{
    [AddComponentMenu("DreamOS/Apps/Notepad/Notepad Backend Storing")]
    public class NotepadStoring : MonoBehaviour
    {
        [Header("Resources")]
        public NotepadManager notepadManager;

        // Note: All backend settings like URL and token are now handled by APIManager.


        #region Response Data Structures
        [System.Serializable]
        public class NoteData
        {
            public string _id;
            public string noteTitle;
            public string noteContent;
            public string ownerId; // New
            public bool isShared;   // New
            public List<NotepadLibrary.SharedUser> sharedWith; // New
        }

        [System.Serializable]
        private class NoteListResponse
        {
            public List<NoteData> notes;
        }

        [System.Serializable]
        private class SingleNoteResponse
        {
            public bool success;
            public NoteData note;
        }
        #endregion

        void Awake()
        {
            if (notepadManager == null)
                notepadManager = gameObject.GetComponent<NotepadManager>();
        }

        void Start()
        {
            // When the scene loads, check if the user is already logged in.
            // If so, automatically fetch their notes.
            if (APIManager.Instance != null && APIManager.Instance.isLoggedIn)
            {
                ReadNoteData();
            }
        }


        #region NEW: Sharing Request Structures
        [System.Serializable]
        private class ShareRequest
        {
            public string targetUserId; // The backend expects an ID, not a username
            public string permission;
        }

        [System.Serializable]
        private class StopShareRequest
        {
            public string targetUserId;
        }
        #endregion
        // This is the public method called by NotepadManager to initiate loading notes.
        public void ReadNoteData()
        {
            if (APIManager.Instance == null)
            {
                Debug.LogError("[NotepadStoring] APIManager not found in the scene.");
                return;
            }
            StartCoroutine(APIManager.Instance.Get("/notes/me", HandleFetchNotesResponse, true));
        }

        private void HandleFetchNotesResponse(UnityWebRequest request)
        {
            if (request == null || request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[NotepadStoring] Failed to fetch notes from server.");
                // If the token is invalid, the APIManager should handle the logout flow.
                if (request != null && (request.responseCode == 401 || request.responseCode == 403))
                {
                    APIManager.Instance.HandleSessionExpired();
                }
                return;
            }

            NoteListResponse response = JsonUtility.FromJson<NoteListResponse>(request.downloadHandler.text);

            // Clear all existing custom notes from the library before loading new ones
            notepadManager.libraryAsset.notes.RemoveAll(note => note.isCustom);
            // Re-initialize to clear the UI before populating it
            notepadManager.PrepareNotes();

            foreach (var noteData in response.notes)
            {
                // Use the manager to create the note UI and add it to the library
                notepadManager.CreateStoredNote(noteData);
            }
        }

        #region NEW: Public Methods for Sharing
        public void ShareNoteOnServer(string noteId, string targetUserId, string permission, Action<bool> callback)
        {
            if (APIManager.Instance == null) { callback?.Invoke(false); return; }

            ShareRequest body = new ShareRequest { targetUserId = targetUserId, permission = permission };
            string jsonBody = JsonUtility.ToJson(body);

            StartCoroutine(APIManager.Instance.Post($"/api/notes/{noteId}/share", jsonBody, (req) =>
            {
                callback?.Invoke(req != null && req.result == UnityWebRequest.Result.Success);
            }, true));
        }

        public void StopSharingNoteOnServer(string noteId, string targetUserId, Action<bool> callback)
        {
            if (APIManager.Instance == null) { callback?.Invoke(false); return; }

            StopShareRequest body = new StopShareRequest { targetUserId = targetUserId };
            string jsonBody = JsonUtility.ToJson(body);

            // Note: The backend route is a POST, not a DELETE, for this action
            StartCoroutine(APIManager.Instance.Post($"/api/notes/{noteId}/stop-sharing", jsonBody, (req) =>
            {
                callback?.Invoke(req != null && req.result == UnityWebRequest.Result.Success);
            }, true));
        }
        #endregion

        // Called by NotepadManager when a user creates a new note in the UI.
        public void CreateNoteOnServer(NotepadLibrary.NoteItem note)
        {
            if (APIManager.Instance == null) return;

            string jsonBody = JsonUtility.ToJson(new NoteData { noteTitle = note.noteTitle, noteContent = note.noteContent });

            // The callback is essential for getting the database ID (_id) back from the server.
            StartCoroutine(APIManager.Instance.Post("/notes", jsonBody, (req) =>
            {
                if (req != null && req.result == UnityWebRequest.Result.Success)
                {
                    SingleNoteResponse response = JsonUtility.FromJson<SingleNoteResponse>(req.downloadHandler.text);
                    note._id = response.note._id; // IMPORTANT: Update the local note object with the ID from the database.
                    Debug.Log($"[NotepadStoring] Successfully created note with ID: {note._id}");
                }
            }, true));
        }

        // Called by NotepadManager when a user edits an existing note.
        public void UpdateNoteOnServer(NotepadLibrary.NoteItem note)
        {
            if (APIManager.Instance == null) return;

            if (string.IsNullOrEmpty(note._id))
            {
                // Failsafe: If we're trying to update a note that somehow wasn't saved to the server yet, create it instead.
                Debug.LogWarning($"[NotepadStoring] Note '{note.noteTitle}' has no ID. Creating it as a new note.");
                CreateNoteOnServer(note);
                return;
            }

            string jsonBody = JsonUtility.ToJson(new NoteData { noteTitle = note.noteTitle, noteContent = note.noteContent });
            StartCoroutine(APIManager.Instance.Put($"/notes/{note._id}", jsonBody, null, true));
        }

        // Called by NotepadManager right before a note is removed from the UI.
        public void DeleteNoteOnServer(string noteId)
        {
            if (APIManager.Instance == null) return;

            if (string.IsNullOrEmpty(noteId))
            {
                Debug.LogWarning("[NotepadStoring] Cannot delete note: Note ID is missing.");
                return;
            }
            StartCoroutine(APIManager.Instance.Delete($"/notes/{noteId}", null, true));
        }
    }
}