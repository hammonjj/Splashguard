using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;

namespace BitBox.Library.Utilities
{
  public static class GameObjectUtils
  {
      public static GameObject GetTopmostParent(GameObject obj)
      {
          var sanityCheck = 0;
          var topmostParent = obj.transform;
          while (topmostParent.parent != null)
          {
              sanityCheck++;
              if (sanityCheck > 1000)
              {
                  throw new Exception("GetTopmostParent: Exceeded sanity check - Possible circular reference in hierarchy.");
              }

              topmostParent = topmostParent.parent;
          }

          return topmostParent.gameObject;
      }

      public static GameObject FindChildWithTag(Transform parent, string tag, bool includeInactive = false)
      {
          if (parent == null || string.IsNullOrEmpty(tag))
          {
              return null;
          }

          var children = parent.GetComponentsInChildren<Transform>(includeInactive);
          foreach (Transform child in children)
          {
              if (child.CompareTag(tag))
              {
                  return child.gameObject;
              }
          }

          Assert.IsTrue(false, $"Child with tag '{tag}' not found under parent '{parent.name}'.");
          return null;
      }

      public static GameObject FindChildWithTag(GameObject parent, string tag, bool includeInactive = false)
      {
          return FindChildWithTag(parent.transform, tag, includeInactive);
      }

      public static List<GameObject> FindChildrenWithTag(Transform parent, string tag, bool includeInactive = false)
      {
          List<GameObject> taggedChildren = new List<GameObject>();

          if (parent == null || string.IsNullOrEmpty(tag))
          {
              return taggedChildren;
          }

          var children = parent.GetComponentsInChildren<Transform>(includeInactive);
          foreach (Transform child in children)
          {
              if (child.CompareTag(tag))
              {
                  taggedChildren.Add(child.gameObject);
              }
          }

          return taggedChildren;
      }

      public static Transform FindChildTransformWithTag(Transform parent, string tag, bool includeInactive = false)
      {
          if (parent == null || string.IsNullOrEmpty(tag))
          {
              return null;
          }

          foreach (Transform child in parent.GetComponentsInChildren<Transform>(includeInactive))
          {
              if (child.CompareTag(tag))
              {
                  return child;
              }
          }

          return null;
      }

      public static int FindClosestTransformIndex(Transform source, Transform[] objects)
      {
          if (source == null || objects == null || objects.Length == 0)
          {
              return -1;
          }

          float closestDistance = float.MaxValue;
          int closestIndex = -1;

          for (int i = 0; i < objects.Length; i++)
          {
              if (objects[i] == null)
              {
                  continue;
              }

              float distance = Vector3.Distance(source.position, objects[i].position);
              if (distance < closestDistance)
              {
                  closestDistance = distance;
                  closestIndex = i;
              }
          }

          return closestIndex;
      }

      public static T FindComponentInParents<T>(GameObject startingPoint, bool includeSelf = true)
      {
          var current = includeSelf ? startingPoint.transform : startingPoint.transform.parent;
          while (current != null)
          {
              T comp = current.GetComponent<T>();
              if (comp != null)
              {
                  return comp;
              }

              current = current.parent;
          }

          return default;
      }

      public static GameObject FindGameObjectInChildrenByName(Transform parent, string name, bool includeInactive = false)
      {
          if (parent == null || string.IsNullOrEmpty(name))
          {
              return null;
          }

          foreach (Transform child in parent)
          {
              if (!includeInactive && !child.gameObject.activeSelf)
              {
                  continue;
              }

              if (child.name.Equals(name, StringComparison.OrdinalIgnoreCase))
              {
                  return child.gameObject;
              }

              var result = FindGameObjectInChildrenByName(child, name, includeInactive);
              if (result != null)
              {
                  return result;
              }
           }

          return null;
      }

    public static bool TryMoveToScene(GameObject gameObject, string sceneName)
    {
        if (gameObject == null || string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        var targetScene = SceneManager.GetSceneByName(sceneName);
        if (!targetScene.IsValid() || !targetScene.isLoaded)
        {
            return false;
        }

        if (gameObject.scene == targetScene)
        {
            return true;
        }

        if (gameObject.transform.parent != null)
        {
            gameObject.transform.SetParent(null, true);
        }

        SceneManager.MoveGameObjectToScene(gameObject, targetScene);
        return true;
    }

    internal static GameObject FindParentWithTag(Transform transform, string combatArenaGeometry)
    {
        var current = transform.parent;
        while (current != null)
        {
            if (current.CompareTag(combatArenaGeometry))
            {
                return current.gameObject;
            }

            current = current.parent;
        }

        throw new Exception($"Parent with tag '{combatArenaGeometry}' not found.");
    }
  }
}
