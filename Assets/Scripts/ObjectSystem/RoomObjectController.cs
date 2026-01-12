// --- START OF FILE RoomObjectController.cs ---
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections;
using System.Collections.Generic;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using UnityEngine.Networking;

public class RoomObjectController : MonoBehaviourPunCallbacks
{
    private const string OBJECTS_LOADED_KEY = "ObjectsLoaded";
    private const int MAX_LOAD_ATTEMPTS = 3;
    private const float RETRY_DELAY_SECONDS = 5.0f;

    private static int _loadAttempts = 0;
    private bool _isLoading = false; // Verrou pour éviter les chargements multiples

    public override void OnJoinedRoom()
    {
        // Seul le Master Client initial charge les objets pour éviter les doublons.
        if (PhotonNetwork.IsMasterClient && !IsRoomInitialized())
        {
            Debug.Log("[RoomObjectController] Je suis le premier Master Client. Je charge les objets de la base de données.");
            StartCoroutine(LoadPersistentObjectsWithRetry());
        }
        else
        {
            Debug.Log("[RoomObjectController] Les objets sont déjà chargés par le Master Client. En attente de la synchronisation de Photon.");
        }
    }

    // --- NOUVELLE MÉTHODE ---
    // Ce callback est appelé sur TOUS les clients lorsqu'un nouveau Master Client est assigné.
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        Debug.Log($"[RoomObjectController] Le Master Client a changé. Le nouveau Master Client est {newMasterClient.NickName}.");

        // Si JE suis le nouveau Master Client, je prends la responsabilité de l'état de la pièce.
        if (newMasterClient.IsLocal)
        {
            Debug.Log("[RoomObjectController] Je suis le nouveau Master Client. Je vais re-synchroniser l'état de la pièce.");

            // Recharger les objets depuis la base de données pour garantir l'état correct.
            // Cela résout le problème des objets détruits de l'ancien Master Client.
            StartCoroutine(LoadPersistentObjectsWithRetry());
        }
    }

    private bool IsRoomInitialized()
    {
        object value;
        return PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(OBJECTS_LOADED_KEY, out value) && (bool)value;
    }

    private IEnumerator LoadPersistentObjectsWithRetry()
    {
        if (_isLoading)
        {
            Debug.LogWarning("[RoomObjectController] Tentative de chargement alors qu'un chargement est déjà en cours. Annulation.");
            yield break;
        }
        _isLoading = true;

        string roomDbId = APIManager.Instance.CurrentRoomDbId;

        if (string.IsNullOrEmpty(roomDbId))
        {
            Debug.Log("[Persistence] Il s'agit d'une chambre d'invité ou sans entrée de base de données. Le chargement d'objets persistants est ignoré.");
            MarkRoomAsInitialized();
            _isLoading = false;
            yield break;
        }

        // --- AMÉLIORATION : Nettoyer les objets existants avant de recharger ---
        // Ceci est crucial pour le nouveau Master Client pour éviter les doublons.
        Debug.Log("[RoomObjectController] Nettoyage des objets persistants existants avant le rechargement...");
        foreach (var ni in FindObjectsOfType<NetworkedInteractable>())
        {
            // Le Master Client a l'autorité de détruire n'importe quel objet.
            PhotonNetwork.Destroy(ni.gameObject);
        }
        // Attendre un court instant pour que la destruction se propage
        yield return new WaitForSeconds(0.5f);

        _loadAttempts = 0;
        while (_loadAttempts < MAX_LOAD_ATTEMPTS)
        {
            _loadAttempts++;
            Debug.Log($"[Persistence] Tentative {_loadAttempts}/{MAX_LOAD_ATTEMPTS}: Chargement des objets pour la pièce ID: {roomDbId}");

            bool isLoadSuccessful = false;
            // Utiliser le PersistenceManager unifié, pas l'APIManager directement pour la cohérence
            PersistenceManager.Instance.LoadRoomObjects(roomDbId, (objectList) => {
                if (objectList != null && objectList.Count > 0)
                {
                    InstantiateObjects(objectList);
                    isLoadSuccessful = true;
                }
                else if (objectList != null) // Liste vide mais succès
                {
                    isLoadSuccessful = true;
                }
            });

            // Attendre la fin de l'appel asynchrone (c'est une simplification, une callback est plus propre)
            // Pour ce cas, on attend simplement que le succès soit marqué.
            float waitTimer = 10f; // Timeout
            while (!isLoadSuccessful && waitTimer > 0)
            {
                yield return null;
                waitTimer -= Time.deltaTime;
            }

            if (isLoadSuccessful)
            {
                Debug.Log("[Persistence] Objets chargés et instanciés avec succès.");
                MarkRoomAsInitialized();
                _isLoading = false;
                yield break; // Sortir de la boucle en cas de succès
            }
            else
            {
                Debug.LogWarning($"[Persistence] La tentative de chargement {_loadAttempts} a échoué.");
                if (_loadAttempts < MAX_LOAD_ATTEMPTS)
                {
                    yield return new WaitForSeconds(RETRY_DELAY_SECONDS);
                }
                else
                {
                    Debug.LogError("[Persistence] Toutes les tentatives de chargement des objets de la pièce ont échoué. La pièce sera vide.");
                }
            }
        }
        _isLoading = false;
    }

    private void InstantiateObjects(List<PersistenceManager.PersistentObjectData> objects)
    {
        if (objects == null) return;

        foreach (var objData in objects)
        {
            Vector3 position = new Vector3(objData.position.x, objData.position.y, objData.position.z);
            Quaternion rotation = new Quaternion(objData.rotation.x, objData.rotation.y, objData.rotation.z, objData.rotation.w);
            object[] instantiationData = new object[] { objData.instanceId };

            // Le Master Client instancie les objets pour tout le monde
            PhotonNetwork.Instantiate(objData.prefabName, position, rotation, 0, instantiationData);
        }
    }

    private void MarkRoomAsInitialized()
    {
        if (PhotonNetwork.IsMasterClient)
        {
            Hashtable props = new Hashtable { { OBJECTS_LOADED_KEY, true } };
            PhotonNetwork.CurrentRoom.SetCustomProperties(props);
            Debug.Log("[Persistence] Pièce marquée comme initialisée.");
        }
    }
}
// --- END OF FILE RoomObjectController.cs ---