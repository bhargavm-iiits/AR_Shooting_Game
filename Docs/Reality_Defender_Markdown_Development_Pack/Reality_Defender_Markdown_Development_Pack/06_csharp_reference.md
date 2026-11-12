# 06 — C# Reference Implementations

These examples are starter implementations for the assignment's core loop. Create each class in a separate `.cs` file with the matching filename. Assign all Inspector references before running.

**Compatibility note:** AR Foundation anchor APIs and Unity input APIs vary by package/editor version. The placement example uses an `ARAnchor` component as a simple anchor workflow; verify the recommended anchor creation pattern for the exact AR Foundation version installed in your project. The examples use the classic `Input` API for compactness. If the project uses the new Input System only, replace the touch polling with Input System actions.

## 1. ARPlacementController.cs

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARPlacementController : MonoBehaviour
{
    [Header("AR")]
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private ARPlaneManager planeManager;
    [SerializeField] private Camera arCamera;

    [Header("Gameplay")]
    [SerializeField] private GameObject gameBasePrefab;
    [SerializeField] private TargetSpawner targetSpawner;

    private static readonly List<ARRaycastHit> Hits = new List<ARRaycastHit>();
    private bool hasPlacedBase;

    private void Update()
    {
        if (hasPlacedBase || Input.touchCount == 0)
            return;

        Touch touch = Input.GetTouch(0);

        if (touch.phase != TouchPhase.Began)
            return;

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject(touch.fingerId))
            return;

        TryPlaceBase(touch.position);
    }

    private void TryPlaceBase(Vector2 screenPosition)
    {
        if (!raycastManager.Raycast(
                screenPosition,
                Hits,
                TrackableType.PlaneWithinPolygon))
            return;

        ARRaycastHit hit = Hits[0];

        Vector3 surfaceUp = hit.pose.up;
        Vector3 projectedForward =
            Vector3.ProjectOnPlane(arCamera.transform.forward, surfaceUp);

        if (projectedForward.sqrMagnitude < 0.001f)
            projectedForward = Vector3.ProjectOnPlane(Vector3.forward, surfaceUp);

        Quaternion rotation =
            Quaternion.LookRotation(projectedForward.normalized, surfaceUp);

        GameObject baseObject = Instantiate(
            gameBasePrefab,
            hit.pose.position,
            rotation);

        // Confirm this component-based anchor workflow against your AR Foundation version.
        if (baseObject.GetComponent<ARAnchor>() == null)
            baseObject.AddComponent<ARAnchor>();

        hasPlacedBase = true;

        if (targetSpawner != null)
            targetSpawner.SpawnTargets(baseObject.transform);

        // Optional: stop further plane updates/visuals after placement.
        // Only do this after confirming the anchor is tracking correctly.
        if (planeManager != null)
            planeManager.enabled = false;
    }
}
```

## 2. TargetSpawner.cs

```csharp
using UnityEngine;

public class TargetSpawner : MonoBehaviour
{
    [SerializeField] private GameObject targetPrefab;
    [SerializeField] private GameManager gameManager;

    [Header("Base-local positions")]
    [SerializeField] private Vector3[] localSpawnPositions =
    {
        new Vector3(-0.35f, 0.55f, 0.00f),
        new Vector3( 0.00f, 0.80f, 0.12f),
        new Vector3( 0.35f, 0.60f, 0.00f)
    };

    public void SpawnTargets(Transform baseTransform)
    {
        if (targetPrefab == null || baseTransform == null)
        {
            Debug.LogError("TargetSpawner is missing a prefab or base transform.");
            return;
        }

        int count = localSpawnPositions.Length;

        if (gameManager != null)
            gameManager.BeginWave(count);

        foreach (Vector3 localPosition in localSpawnPositions)
        {
            Vector3 worldPosition = baseTransform.TransformPoint(localPosition);

            GameObject targetObject = Instantiate(
                targetPrefab,
                worldPosition,
                baseTransform.rotation);

            Target target = targetObject.GetComponent<Target>();

            if (target == null)
                Debug.LogError("Target prefab needs a Target component.");
            else
                target.SetGameManager(gameManager);
        }
    }
}
```

## 3. GameManager.cs

```csharp
using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public event Action<int> ScoreChanged;
    public event Action WaveCleared;

    public int Score { get; private set; }
    public int RemainingTargets { get; private set; }

    public void BeginWave(int targetCount)
    {
        Score = 0;
        RemainingTargets = targetCount;
        ScoreChanged?.Invoke(Score);

        if (targetCount <= 0)
            CompleteWave();
    }

    public void RegisterTargetHit(int points)
    {
        if (RemainingTargets <= 0)
            return;

        Score += points;
        RemainingTargets--;

        ScoreChanged?.Invoke(Score);

        if (RemainingTargets == 0)
            CompleteWave();
    }

    private void CompleteWave()
    {
        WaveCleared?.Invoke();
    }
}
```

## 4. Target.cs

```csharp
using UnityEngine;

public class Target : MonoBehaviour
{
    [SerializeField] private GameObject hitVfxPrefab;
    [SerializeField] private int points = 10;

    private GameManager gameManager;
    private bool hasBeenDestroyed;

    public void SetGameManager(GameManager manager)
    {
        gameManager = manager;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasBeenDestroyed)
            return;

        Projectile projectile =
            collision.collider.GetComponentInParent<Projectile>();

        if (projectile == null)
            return;

        hasBeenDestroyed = true;

        if (hitVfxPrefab != null)
        {
            GameObject effect = Instantiate(
                hitVfxPrefab,
                transform.position,
                Quaternion.identity);

            ParticleSystem particles = effect.GetComponent<ParticleSystem>();

            if (particles != null)
                Destroy(effect, particles.main.duration + particles.main.startLifetime.constantMax + 0.25f);
            else
                Destroy(effect, 3f);
        }

        if (gameManager != null)
            gameManager.RegisterTargetHit(points);

        Destroy(projectile.gameObject);
        Destroy(gameObject);
    }
}
```

## 5. WeaponController.cs

```csharp
using UnityEngine;

public class WeaponController : MonoBehaviour
{
    [SerializeField] private Camera arCamera;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float muzzleOffset = 0.15f;
    [SerializeField] private float projectileSpeed = 12f;

    public void Fire()
    {
        if (arCamera == null || projectilePrefab == null)
        {
            Debug.LogWarning("WeaponController is missing a camera or projectile prefab.");
            return;
        }

        Transform cameraTransform = arCamera.transform;

        Vector3 spawnPosition =
            cameraTransform.position + cameraTransform.forward * muzzleOffset;

        GameObject projectileObject = Instantiate(
            projectilePrefab,
            spawnPosition,
            cameraTransform.rotation);

        Rigidbody body = projectileObject.GetComponent<Rigidbody>();

        if (body == null)
        {
            Debug.LogError("Projectile prefab needs a Rigidbody.");
            Destroy(projectileObject);
            return;
        }

        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.velocity = cameraTransform.forward * projectileSpeed;

        Projectile projectile =
            projectileObject.GetComponent<Projectile>();

        if (projectile != null)
            projectile.SetOrigin(spawnPosition);
    }
}
```

## 6. Projectile.cs

```csharp
using UnityEngine;

public class Projectile : MonoBehaviour
{
    [SerializeField] private float maxTravelDistance = 5f;

    private Vector3 origin;
    private bool originSet;

    public void SetOrigin(Vector3 spawnPosition)
    {
        origin = spawnPosition;
        originSet = true;
    }

    private void Start()
    {
        if (!originSet)
            origin = transform.position;
    }

    private void Update()
    {
        if (Vector3.Distance(origin, transform.position) > maxTravelDistance)
            Destroy(gameObject);
    }
}
```

## 7. ScoreUI.cs

```csharp
using TMPro;
using UnityEngine;

public class ScoreUI : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private GameObject waveClearedLabel;

    private void OnEnable()
    {
        if (gameManager == null)
            return;

        gameManager.ScoreChanged += UpdateScore;
        gameManager.WaveCleared += ShowWaveCleared;
    }

    private void OnDisable()
    {
        if (gameManager == null)
            return;

        gameManager.ScoreChanged -= UpdateScore;
        gameManager.WaveCleared -= ShowWaveCleared;
    }

    private void Start()
    {
        if (waveClearedLabel != null)
            waveClearedLabel.SetActive(false);

        if (gameManager != null)
            UpdateScore(gameManager.Score);
    }

    private void UpdateScore(int score)
    {
        if (scoreText != null)
            scoreText.text = "SCORE  " + score;
    }

    private void ShowWaveCleared()
    {
        if (waveClearedLabel != null)
            waveClearedLabel.SetActive(true);
    }
}
```

## 8. Inspector wiring

### ARPlacementController
- Raycast Manager → XR Origin's `ARRaycastManager`
- Plane Manager → XR Origin's `ARPlaneManager`
- AR Camera → AR camera
- Game Base Prefab → Game Base prefab
- Target Spawner → scene `TargetSpawner`

### TargetSpawner
- Target Prefab → target prefab
- Game Manager → scene `GameManager`
- Confirm exactly three local positions are configured.

### WeaponController
- AR Camera → AR camera
- Projectile Prefab → projectile prefab
- FIRE button → OnClick → `WeaponController.Fire()`

### Target prefab
- Collider: enabled, `Is Trigger` off
- `Target` component: enabled
- Hit VFX Prefab: optional particle prefab
- Renderer: custom Shader Graph material

### Projectile prefab
- Rigidbody: enabled, gravity off
- Collider: enabled, `Is Trigger` off
- `Projectile` component: enabled

### ScoreUI
- Game Manager → scene `GameManager`
- Score Text → TMP score label
- Wave Cleared Label → completion UI object

## 9. Important implementation checks

- A collision callback requires at least one Rigidbody in the collision pair. The projectile Rigidbody satisfies this for static/kinematic target colliders.
- Check that the target and projectile layers are allowed to collide in **Project Settings → Physics**.
- Use a collider size that matches the target's visible shape.
- Prevent target double-scoring with the `hasBeenDestroyed` guard.
- Ensure the hit effect is instantiated before the target is destroyed.
- If projectiles pass through targets at high speed, lower speed, enlarge colliders, or use continuous collision detection.
- If using a target Rigidbody, set it kinematic unless physics-driven movement is intended.
- The examples are a baseline; test on a physical device and adapt anchor/input APIs to the selected package versions.
