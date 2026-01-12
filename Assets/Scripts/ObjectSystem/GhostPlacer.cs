// --- START OF FILE GhostPlacer.cs ---
using UnityEngine;
using Photon.Pun;
using System.Collections;

public class GhostPlacer : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The camera used for raycasting mouse position.")]
    [SerializeField] private Camera mainCamera;

    [Header("Placement Settings")]
    [Tooltip("Colliders on these layers will block placement.")]
    [SerializeField] private LayerMask placementCollisionMask;
    [Tooltip("The radius to check for collisions when validating a placement spot.")]
    [SerializeField] private float placementValidationRadius = 0.5f;
    [SerializeField] private Color validPlacementColor = new Color(0, 1, 0, 0.5f);
    [SerializeField] private Color invalidPlacementColor = new Color(1, 0, 0, 0.5f);

    private GameObject _ghost;
    private CatalogItem _itemToPlace;
    private SpriteRenderer _ghostSpriteRenderer;
    private bool _isPlacing = false;
    private bool _canPlace = false;

    void Update()
    {
        if (!_isPlacing) return;

        Vector3 worldPos = GetMouseWorldPosition();
        if (_ghost != null)
        {
            _ghost.transform.position = worldPos;
            ValidatePlacement(worldPos);
        }

        if (Input.GetMouseButtonDown(0) && _canPlace)
        {
            ConfirmPlacement(worldPos);
        }
        else if (Input.GetMouseButtonDown(1))
        {
            CancelPlacement();
        }
    }

    public void BeginPlacing(CatalogItem item)
    {
        if (_isPlacing || item == null || item.networkPrefab == null) return;
        _itemToPlace = item;
        _isPlacing = true;
        _ghost = Instantiate(_itemToPlace.networkPrefab);

        foreach (var view in _ghost.GetComponentsInChildren<PhotonView>()) Destroy(view);
        foreach (var interactable in _ghost.GetComponentsInChildren<NetworkedInteractable>()) Destroy(interactable);

        _ghostSpriteRenderer = _ghost.GetComponentInChildren<SpriteRenderer>();
        if (_ghostSpriteRenderer != null)
        {
            _ghostSpriteRenderer.color = invalidPlacementColor;
        }
    }

    private void ValidatePlacement(Vector3 position)
    {
        var collider = Physics2D.OverlapCircle(position, placementValidationRadius, placementCollisionMask);
        _canPlace = (collider == null);
        if (_ghostSpriteRenderer != null)
        {
            _ghostSpriteRenderer.color = _canPlace ? validPlacementColor : invalidPlacementColor;
        }
    }

    private void ConfirmPlacement(Vector3 position)
    {
        if (_itemToPlace == null) return;

        // --- CHANGEMENT 1: Capturer les données nécessaires AVANT d'appeler Cleanup ---
        string prefabNameToSave = _itemToPlace.networkPrefab.name;
        string instanceId = System.Guid.NewGuid().ToString();
        object[] instantiationData = new object[] { instanceId };

        GameObject newNetworkObject = PhotonNetwork.Instantiate(
            _itemToPlace.networkPrefab.name, // On utilise toujours la référence ici pour l'instanciation
            position,
            Quaternion.identity,
            0,
            instantiationData
        );

        // S'assurer que l'objet a bien été créé avant d'essayer de le sauvegarder
        if (newNetworkObject != null)
        {
            // --- CHANGEMENT 2: Passer la donnée capturée à la coroutine ---
            StartCoroutine(SaveObjectAfterDelay(instanceId, newNetworkObject, prefabNameToSave));
        }
        else
        {
            Debug.LogError($"PhotonNetwork.Instantiate a échoué pour le prefab '{prefabNameToSave}'. L'objet ne sera pas sauvegardé.");
        }

        Cleanup(); // Cleanup() est maintenant sans danger
    }

    // --- CHANGEMENT 3: Mettre à jour la signature de la coroutine ---
    private IEnumerator SaveObjectAfterDelay(string instanceId, GameObject newNetworkObject, string prefabName)
    {
        yield return null; // Attendre la prochaine frame est toujours une bonne pratique

        // --- AMÉLIORATION: Ajouter des gardes pour un débogage plus facile ---
        if (APIManager.Instance == null)
        {
            Debug.LogError("[GhostPlacer] Impossible de sauvegarder l'objet, APIManager.Instance est null !");
            yield break; // Arrêter la coroutine
        }
        if (PersistenceManager.Instance == null)
        {
            Debug.LogError("[GhostPlacer] Impossible de sauvegarder l'objet, PersistenceManager.Instance est null !");
            yield break; // Arrêter la coroutine
        }
        if (newNetworkObject == null)
        {
            Debug.LogError($"[GhostPlacer] Impossible de sauvegarder l'objet car la référence à l'objet instancié est nulle (instanceId: {instanceId}).");
            yield break;
        }

        string roomDbId = APIManager.Instance.CurrentRoomDbId;
        if (!string.IsNullOrEmpty(roomDbId))
        {
            PersistenceManager.Instance.SaveNewObject(
                roomDbId,
                instanceId,
                prefabName, // --- CHANGEMENT 4: Utiliser la variable passée en paramètre ---
                newNetworkObject.transform.position,
                newNetworkObject.transform.rotation
            );
        }
    }


    private void CancelPlacement() => Cleanup();

    private void Cleanup()
    {
        if (_ghost != null) Destroy(_ghost);
        _ghost = null;
        _itemToPlace = null;
        _isPlacing = false;
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mousePos = Input.mousePosition;
        Plane groundPlane = new Plane(Vector3.forward, Vector3.zero);
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        if (groundPlane.Raycast(ray, out float distance))
        {
            return ray.GetPoint(distance);
        }
        return Vector3.zero;
    }
}
// --- END OF FILE GhostPlacer.cs ---