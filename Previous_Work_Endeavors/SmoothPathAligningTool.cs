using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.IO;
using System.Linq;
using System;
using Cinemachine;

public class SmoothPathAligningTool : EditorWindow
{
    private CinemachineSmoothPath _cinePath;
    private SmoothPathBox _smoothPathBox;
    private ParentSpacePath _parentPath;

    private List<SmoothPathToBeAlligned> _objectsToBeAlligned = new List<SmoothPathToBeAlligned>();

    private List<PathPickups> _pathsPickups = new List<PathPickups>();

    bool _isAlternatePath;


    [MenuItem("Window/Smooth Path Aligning Tool")]
    public static void ShowWindow()
    {
        GetWindow<SmoothPathAligningTool>("Smooth Path Aligning Tool");
    }

    private void OnGUI()
    {
        ShowButtons();
    }

    public void ShowButtons()
    {
        // button parenting objects
        AllignObjectsWithinSmoothBoxToSelectedPath();
    }

    private void AllignObjectsWithinSmoothBoxToSelectedPath()
    {
        if (GUILayout.Button("Step 1 : Select a Path"))
        {
            // find the path to which we want to lock our obstacles/pickups to
            GameObject selectedObject = Selection.activeGameObject;
            _cinePath = selectedObject.GetComponent<CinemachineSmoothPath>();

            if (_cinePath != null)
            {

            }
            else if (selectedObject.GetComponentInChildren<CinemachineSmoothPath>() != null)
            {
                _cinePath = selectedObject.GetComponentInChildren<CinemachineSmoothPath>();
            }
            else
            {
                Debug.LogWarning("no Cinemachine Path found !");
            }
        }

        if (GUILayout.Button("Step 2 : Select The SmoothBox"))
        {
            // find the path to which we want to lock our obstacles/pickups to
            GameObject selectedObject = Selection.activeGameObject;
            _smoothPathBox = selectedObject.GetComponent<SmoothPathBox>();

            if (_smoothPathBox != null)
            {

            }
            else
            {
                Debug.LogWarning("no SmoothBox found !");
            }


            // clear the list
            _smoothPathBox.EmptyTheList();

            // check for all objects(with smoothPathToBeAlligned) in the scene if they fit into this box's bounds
            // if so -> add to smoothbox list
            _objectsToBeAlligned = FindObjectsOfType<SmoothPathToBeAlligned>().ToList();
            Bounds BoxBounds = _smoothPathBox.GetComponent<Collider>().bounds;

            for (int i =0; i < _objectsToBeAlligned.Count; i++)
            {
                var objectOfInterest = _objectsToBeAlligned[i];

                if (BoxBounds.Contains(objectOfInterest.transform.position))
                {
                    _smoothPathBox.ObjectsToBeAlligned.Add(objectOfInterest);
                }
            }
        }

        if (GUILayout.Button("Step 3 : Select the correct Path parent"))
        {
            GameObject selectedObject = Selection.activeGameObject;
            _parentPath = selectedObject.GetComponent<ParentSpacePath>();

            if (_parentPath != null)
            {

            }
            else
            {
                Debug.LogWarning("no Parent Space Path found on clicked object ! Parenting can go wrong !!!");
            }
        }



        if (GUILayout.Button("Step 4 : Allign objects in Smoothbox to Path"))
        {
            if (FindObjectsOfType<ParentSpacePath>().Length > 1)
            {
                Debug.LogWarning("more than 1 ParentSpacePath gameobject is enabled! make sure only 1 is active and try again");
                return;
            }

            if (_cinePath != null && _smoothPathBox != null)
            {
                var tempListForParenting = new List<SmoothPathToBeAlligned>();

                // check if selected path is an alternate path
                _isAlternatePath = false;
                if (_cinePath.GetComponentInChildren<SwitchPath>() != null)
                {
                    _isAlternatePath = true;
                }

                // check each object and if possible, allign it to the selected path
                for (int i = 0; i < _smoothPathBox.ObjectsToBeAlligned.Count; i++)
                {
                    var objectToAlign = _smoothPathBox.ObjectsToBeAlligned[i];

                    // do not allign the object if : 1) I am an alternate path + 2) the object is an alternate path 
                    // (current logic would make it impossible to allign a split path with another split path)
                    if (_isAlternatePath == true && objectToAlign.GetComponentInChildren<SwitchPath>() != null)
                    {
                        // do not allign the object
                    }
                    else
                    {
                        tempListForParenting.Add(objectToAlign);
                        float closestPathFloat = _cinePath.FindClosestPoint(objectToAlign.transform.position, 0, -1, 20);

                        Vector3 closestPathPosition = _cinePath.EvaluatePosition(closestPathFloat);
                        Quaternion closestPathQuaternion = _cinePath.EvaluateOrientation(closestPathFloat);

                        objectToAlign.transform.position = closestPathPosition;

                        // do not rotate the paths (cinemachine doesnt properly work with rotated paths) !!!
                        if (objectToAlign.GetComponentInChildren<CinemachineSmoothPath>() == null)
                        {
                            objectToAlign.transform.rotation = closestPathQuaternion;
                        }
                        else if (objectToAlign.TryGetComponent(out PathPickups pathPickups) == true) // path pickups are allowed to be alligned
                        {
                            objectToAlign.transform.rotation = closestPathQuaternion;
                        }
                        else
                        {
                            // paths are set to 0,0,0.  the entry trigger however is alligned with the path
                            objectToAlign.transform.rotation = Quaternion.Euler(0, 0, 0);
                            objectToAlign.GetComponentInChildren<AttachWaypointsToPath>().StartTrigger.transform.rotation = closestPathQuaternion;
                        }
                    }

                    EditorUtility.SetDirty(_smoothPathBox.ObjectsToBeAlligned[i]);
                }


                // parent the stuff in the proper transforms, in a proper order
                if (_parentPath != null)
                {
                    // first unparent them all
                    foreach (var obj in tempListForParenting)
                    {
                        obj.transform.SetParent(null);
                    }

                    // then parent them accordingly
                    foreach (var obj in tempListForParenting.OrderBy(v => v.transform.position.z))
                    {
                        // DO NOT parent split paths, DO parent paths that have pickups
                        if (obj.GetComponentInChildren<CinemachineSmoothPath>() != null && obj.GetComponent<PathPickups>() == null)
                        {
                            // do nothing
                        }
                        else if (obj.GetComponentInChildren<BoostRing>() != null)
                        {
                            obj.transform.SetParent(_parentPath.ParentBoosts.transform);
                        }
                        else if (obj.GetComponentInChildren<PickUp>() != null)
                        {
                            obj.transform.SetParent(_parentPath.ParentPickups.transform);
                        }
                        else if (obj.GetComponentInChildren<ObstacleCollision>() != null)
                        {
                            obj.transform.SetParent(_parentPath.ParentObstacles.transform);
                        }
                    }
                }
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
            else
            {
                Debug.LogWarning("no path or no box assigned !");
            }
        }

        if (GUILayout.Button("SHOW Visuals for Player bounding boxes"))
        {
            _objectsToBeAlligned = FindObjectsOfType<SmoothPathToBeAlligned>().ToList();

            for (int i = 0; i < _objectsToBeAlligned.Count; i++)
            {
                _objectsToBeAlligned[i].ShowVisuals(true);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        if (GUILayout.Button("HIDE Visuals for Player bounding boxes"))
        {
            _objectsToBeAlligned = FindObjectsOfType<SmoothPathToBeAlligned>().ToList();

            for (int i = 0; i < _objectsToBeAlligned.Count; i++)
            {
                _objectsToBeAlligned[i].ShowVisuals(false);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }


        if (GUILayout.Button("Create pickups on the pickup-paths"))
        {
            // find objects with a specific script 'PathPickup' (has a list of pickups 'A', has a list of waypoints 'B')
            _pathsPickups = FindObjectsOfType<PathPickups>().ToList();

            // for each path... 
            for (int i = 0; i < _pathsPickups.Count; i++)
            {
                // access the list 'A', destroy each object in the list -> clear the list.
                var pathOfInterest = _pathsPickups[i];

                // destroy all previous pickups 
                pathOfInterest.DestroyMyChildrenPickups();

                // store the current rotation of the path + parent obj, set path.rot + obj.rot to 0,0,0
                pathOfInterest.StoreRotations();
                pathOfInterest.ResetRotations();

                // clear the list 'B', add each waypoint to it.
                pathOfInterest.GetWaypoints();

                // for each obj in 'B' -> add + instantiate a pickup to 'A'
                pathOfInterest.CreatePickups();

                // reset the rotation to the stored value
                pathOfInterest.RevertRotations();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        if (GUILayout.Button("Delete pickups on the pickup-paths"))
        {
            // find objects with a specific script 'PathPickup' (has a list of pickups 'A', has a list of waypoints 'B')
            _pathsPickups = FindObjectsOfType<PathPickups>().ToList();

            // for each path... 
            for (int i = 0; i < _pathsPickups.Count; i++)
            {
                // clear the list, destroy all children of smoothpath.
                var pathOfInterest = _pathsPickups[i];

                pathOfInterest.DestroyMyChildrenPickups();
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }
    }
}
