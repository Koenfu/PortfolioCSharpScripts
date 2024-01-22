using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using System.IO;
using System.Linq;

public class SectionCreationTool : EditorWindow
{
    public LevelSectionCreator SelectedLevelSectionCreator;
    public Section SelectedSection;

    private List<LevelSectionCreator> _sectionCreatorsInScene = new List<LevelSectionCreator>();
    private List<LevelSectionCreator> _sectionCreatorsToCheckOverlap = new List<LevelSectionCreator>();
    private List<LevelSectionCreator> _sectionCreatorsStructured = new List<LevelSectionCreator>();

    private string[] _toolbarStrings = { "Creation", "Analyze" };
    int _toolbarSelected = 0;

    private string _adjustedSceneString = string.Empty;

    private const string _localPathPrefix = "Assets/Prefabs/Prefabs_Sections/Prefabs_Sections_Level_0";
    private const string _localPathMidfix = "/Scene_";
    private const string _localPathSuffix = "/Resources/";

    private int _totalGameobjects;
    private int _totalTriangles;
    private int _totalVertices;
    private int _totalPickups;


    [MenuItem("Window/Section Tool")]
    public static void ShowWindow()
    {
        GetWindow<SectionCreationTool>("Section Tools");
    }

    private void OnGUI()
    {
        GUILayout.BeginHorizontal();
        _toolbarSelected = GUILayout.Toolbar(_toolbarSelected, _toolbarStrings);
        GUILayout.EndHorizontal();

        ShowButtons();
    }

    public void ShowButtons()
    {
        // Get selected object in hierarchy
        SelectedLevelSectionCreator = null;
        SelectedSection = null;

        GameObject selectedObject = Selection.activeGameObject;
        if (selectedObject != null)
        {
            SelectedLevelSectionCreator = selectedObject.GetComponent<LevelSectionCreator>();

            if (SelectedLevelSectionCreator != null)
            {
                SelectedSection = SelectedLevelSectionCreator.Section;
            }
            else
            {
                SelectedSection = selectedObject.GetComponent<Section>();
            }
        }
   
        if (_toolbarSelected == 0)
        {
            // button parenting objects
            ParentObjectsWithinColliders();
        }
        else
        {
            // buttons analyzing data
            AnalyzeDataInSection();
        }
    }


    private void ParentObjectsWithinColliders()
    {
        if (GUILayout.Button("Create Section Prefabs"))
        {
            // 0) reset lists in case of scene change
            _sectionCreatorsToCheckOverlap.Clear();
            _sectionCreatorsStructured.Clear();
            // 0.1) adjust scene string so level section don't have "Work" in their names
            AdjustSceneString();

            // 1) Find the blockout Parent
            BlockoutParent blockoutParent = FindObjectOfType<BlockoutParent>();
            if (blockoutParent == null)
            {
                Debug.LogWarning("Could not find a BlockoutParent script in the scene!");
                return;
            }

            // 2) Find the sectionCreatorPrefabs and unpack them
            _sectionCreatorsInScene = FindObjectsOfType<LevelSectionCreator>().ToList();
            for (int i = 0; i < _sectionCreatorsInScene.Count; i++)
            {
                if (PrefabUtility.IsAnyPrefabInstanceRoot(_sectionCreatorsInScene[i].gameObject) == true)
                {
                    // this being registered as a user action allows it to be Undo-d
                    PrefabUtility.UnpackPrefabInstance(_sectionCreatorsInScene[i].gameObject, PrefabUnpackMode.OutermostRoot,
                        InteractionMode.UserAction);
                }
            }
            // 2.1) assign starting section
            LevelSectionCreator sectionZero = null;
            int amountOfStartingSectionsFound = 0;
            for (int i = 0; i < _sectionCreatorsInScene.Count; i++)
            {
                if (_sectionCreatorsInScene[i].IsStartingSection)
                {
                    sectionZero = _sectionCreatorsInScene[i];
                    amountOfStartingSectionsFound += 1;
                }
            }
            if (amountOfStartingSectionsFound == 0)
            {
                Debug.LogWarning("No Starting Section found! make sure atleast 1 has the bool IsStart 'checked'");
                // undo-ing the unpacking of the section creators
                Undo.PerformUndo();
                return;
            }
            else if (amountOfStartingSectionsFound > 1)
            {
                Debug.LogWarning("Found more than 1 Starting Section! make sure only 1 has the bool IsStart 'checked'");
                // undo-ing the unpacking of the section creators
                Undo.PerformUndo();
                return;
            }

            // 2.2) use list of sections we need to check for collision
            for (int i = 0; i < _sectionCreatorsInScene.Count; i++)
            {
                _sectionCreatorsToCheckOverlap.Add(_sectionCreatorsInScene[i]);
            }
            // 2.3) Remove starting section from sectionCreatorsToCheck
            _sectionCreatorsToCheckOverlap.Remove(sectionZero);
            // 2.4) Add SectionZero to structured list
            _sectionCreatorsStructured.Add(sectionZero);
            // 2.5) use the starting section to progressively find following sections by checking the heads
            FindFollowingSectionCreator(sectionZero);

            // 3) Next up, get all objects, duplicate them, add to temp list, check tempList objects for inside of bounds, parent objects
            // 3.1) duplicate the blockout_parent
            BlockoutParent blockoutCopy = Instantiate(blockoutParent);
            // 3.2) disable the original blockout
            blockoutParent.gameObject.SetActive(false);

            // 3.3) add all children of the copy to a temp list
            List<GameObject> tempList = new List<GameObject>();
            ParentAllTheThings(tempList, blockoutCopy.transform);

            // 4) iterate over each sectionCreator... 
            for (int i = 0; i < _sectionCreatorsStructured.Count; i++)
            {
                // 4.1) iterate over each collider...
                foreach (BoxCollider coll in _sectionCreatorsStructured[i].CollidersDefiningMySection)
                {
                    // 4.2) parent any objects that fall within the bounds (only check first children of the blockout)
                    List<GameObject> objectsAdded = new List<GameObject>();
                    foreach (GameObject obj in tempList)
                    {
                        if (coll.bounds.Contains(obj.transform.position))
                        {
                            // check if the object has pickups its children
                            // if so -> add the children instead of the obj itself
                            List<PickUp> pickupsFound = obj.GetComponentsInChildren<PickUp>(true).ToList();
                            if (pickupsFound.Count > 0)
                            {
                                GameObject pickupChild = null;
                                for (int k = 0; k < pickupsFound.Count; k++)
                                {
                                    pickupChild = pickupsFound[k].gameObject;

                                    pickupChild.transform.SetParent(_sectionCreatorsStructured[i].Section.PickupsParent.gameObject.transform);
                                    objectsAdded.Add(pickupChild);
                                }
                                continue;
                            }

                            // any other gameobject that does not have pickups in its children
                            objectsAdded.Add(obj);

                            // differentiate between pickups and environment
                            if (obj.TryGetComponent(out PickUp pickUp))
                            {
                                obj.transform.SetParent(_sectionCreatorsStructured[i].Section.PickupsParent.gameObject.transform);
                            }
                            else
                            {
                                obj.transform.SetParent(_sectionCreatorsStructured[i].Section.ParentEnvironment
                                    .transform);
                            }
                        }
                    }

                    // 4.3) remove previously added objects from tempList (performance tweak)
                    foreach (GameObject obj in objectsAdded)
                    {
                        if (tempList.Contains(obj))
                        {
                            tempList.Remove(obj);
                        }
                    }
                    objectsAdded.Clear();
                }

                // 4.4) name our section something fitting
                _sectionCreatorsStructured[i].Section.name = "PV_LevelSection_" + _adjustedSceneString + i;
                _sectionCreatorsStructured[i].Section.ParentEnvironment.name = "Environment_" + i;
                _sectionCreatorsStructured[i].Section.PickupsParent.gameObject.name = "Pickups_" + i;

                // 4.5) log data regarding the section into the console
                LogCurrentSectionData(_sectionCreatorsStructured[i].Section);
            }
            LogAllData();

            Debug.Log("finished parenting blockout");

            // 5) get correct directory dependant on our scene
            string levelIndexString = DecodeSceneString()[0].ToString();
            // useful for if we want to divide prefab sections into even more folders
            string levelSceneIndexString = DecodeSceneString()[1].ToString();

            string localPath = _localPathPrefix + levelIndexString + _localPathMidfix + levelSceneIndexString + _localPathSuffix;
            // 5.1) delete folder and its contents, then Re-create it
            if (Directory.Exists(localPath))
            {
                Directory.Delete(localPath, true);
            }
            Directory.CreateDirectory(localPath);

            // 6) create prefab of each section
            for (int i = 0; i < _sectionCreatorsStructured.Count; i++)
            {
                Section sectionOfInterest = _sectionCreatorsStructured[i].Section;

                // 6.1) get the Section of interest, Unparent it (prefab will remember it's world position this way)
                GameObject objectToPrefabify = sectionOfInterest.gameObject;
                objectToPrefabify.transform.SetParent(null);

                // 6.2) Create the new Prefab.
                PrefabUtility.SaveAsPrefabAsset(objectToPrefabify, localPath + objectToPrefabify.name + ".prefab");

                // 6.3) Destroy all the children in environment and pickups (wipe clean for a scene reset) of current Section
                for (int j = sectionOfInterest.ParentEnvironment.transform.childCount - 1; j >= 0; j--)
                {
                    DestroyImmediate(sectionOfInterest.ParentEnvironment.transform.GetChild(j).gameObject);
                }
                for (int j = sectionOfInterest.PickupsParent.transform.childCount - 1; j >= 0; j--)
                {
                    DestroyImmediate(sectionOfInterest.PickupsParent.transform.GetChild(j).gameObject);
                }

                // 6.4) Re-parent it to its creator
                objectToPrefabify.transform.SetParent(_sectionCreatorsStructured[i].transform);
            }

            // 7) reset the scene by destroying duplicates
            // 7.1) Re-enable the old blockout
            blockoutParent.gameObject.SetActive(true);
            // 7.2) destroy copied blockout (should ideally be empty object after all of its children have been re-parented)
            DestroyImmediate(blockoutCopy.gameObject);

            // 8) undo-ing the unpacking of the section creators
            Undo.PerformUndo();
        }
    }

    private void LogCurrentSectionData(Section sectionToLog)
    {
        if (sectionToLog != null)
        {
            int countObjects = 0;
            int countTriangles = 0;
            int countVertices = 0;
            int countPickups = 0;

            // add up the objects, tris, and pickups

            // objects
            countObjects += GetChildren(sectionToLog.ParentEnvironment);
            countObjects += GetChildren(sectionToLog.PickupsParent.gameObject);

            // tris
            countTriangles += GetTriangleCount(sectionToLog.ParentEnvironment);
            countTriangles += GetTriangleCount(sectionToLog.PickupsParent.gameObject);

            // vertices
            countVertices += GetVertexCount(sectionToLog.ParentEnvironment);
            countVertices += GetVertexCount(sectionToLog.PickupsParent.gameObject);

            // pickUps
            countPickups += GetPickupCount(sectionToLog.PickupsParent.gameObject);

            _totalGameobjects += countObjects;
            _totalTriangles += countTriangles;
            _totalVertices += countVertices;
            _totalPickups += countPickups;

            Debug.Log(sectionToLog.name + " has " + countObjects + " gameobjects !");
            Debug.Log(sectionToLog.name + " has " + countTriangles + " Triangles !");
            Debug.Log(sectionToLog.name + " has " + countVertices + " Vertices !");
            Debug.Log(sectionToLog.name + " has " + countPickups + " Pickups !");
        }
    }
    private void LogAllData()
    {
        Debug.Log("The current scene has " + _totalGameobjects + " gameobjects !");
        Debug.Log("The current scene has " + _totalTriangles + " Triangles !");
        Debug.Log("The current scene has " + _totalVertices + " Vertices !");
        Debug.Log("The current scene has " + _totalPickups + " Pickups !");

        _totalGameobjects = 0;
        _totalTriangles = 0;
        _totalVertices = 0;
        _totalPickups = 0;
    }

    private void ParentAllTheThings(List<GameObject> listToAddTo, Transform objTransformToCheckChildren)
    {
        for (int i = 0; i < objTransformToCheckChildren.transform.childCount; i++)
        {
            GameObject objToAdd = objTransformToCheckChildren.transform.GetChild(i).gameObject;

            // if the object we are checking is a parent-structure, then re-do this methods logic on that child (in case paren-structures are nested in each other)
            if (objToAdd.TryGetComponent(out ParentStructure parentPurelyForStructure))
            {      
                ParentAllTheThings(listToAddTo, parentPurelyForStructure.transform);
                continue;
            }

            // else, we simply add the object to the list
            listToAddTo.Add(objToAdd);
        }
    }

    private void FindFollowingSectionCreator(LevelSectionCreator currentSectionCreatorToCheck)
    {        
        var headBounds = currentSectionCreatorToCheck.ColliderHead.bounds;
     
        bool headHasCollided = false;
        LevelSectionCreator succeedingSection = null;
        for (int i = 0; i < _sectionCreatorsToCheckOverlap.Count; i++)
        {
            // if the head collides with even 1 collider, then we have found our following section
            for (int j = 0; j < _sectionCreatorsToCheckOverlap[i].CollidersDefiningMySection.Count; j++)
            {
                var sectionBounds = _sectionCreatorsToCheckOverlap[i].CollidersDefiningMySection[j].bounds;
                if (headBounds.Intersects(sectionBounds) == true)
                {
                    headHasCollided = true;
                    succeedingSection = _sectionCreatorsToCheckOverlap[i];
                    break;
                }
            }

            if (headHasCollided)
            {
                break;
            }
        }

        if (headHasCollided == true)
        {
            // remove the found section from sections to check
            _sectionCreatorsToCheckOverlap.Remove(succeedingSection);
            // add to structured list
            _sectionCreatorsStructured.Add(succeedingSection);
            // re-do the logic & pass through the found section
            FindFollowingSectionCreator(succeedingSection);
        }
        else
        {
            Debug.Log("Finished structuring Sections");
        }
    }



    private List<int> DecodeSceneString()
    {
        // Split myString wherever there's a _ and make a String array out of it.
        string[] stringArray = SceneManager.GetActiveScene().name.Split("_"[0]);

        List<int> numbersInSceneName = new List<int>();
        for (int num = 0; num < stringArray.Length; num++)
        {
            if (int.TryParse(stringArray[num], out int foundInt) == true)
            {
                numbersInSceneName.Add(foundInt);
            }        
        }

        return numbersInSceneName;
    }
    private void AdjustSceneString()
    {
        // Split myString wherever there's a _ and make a String array out of it.
        string[] stringArray = SceneManager.GetActiveScene().name.Split("_"[0]);

        foreach (var stringVar in stringArray)
        {
            Debug.Log(stringVar + " a piece of my scene name");
        }

        // remove the work from the string name 
        if (_adjustedSceneString == string.Empty)
        {
            for (int i = 0; i < stringArray.Length - 1; i++) // (Length - 1)=> will exclude the word "Work"
            {
                _adjustedSceneString += (stringArray[i] + "_");
            }
        }
        Debug.Log(_adjustedSceneString + " this is the adjusted scene string");
    }

    private void AnalyzeDataInSection()
    {
        // log section GameObjects
        if (GUILayout.Button("log Selected Section GamObjects"))
        {
            if (SelectedSection != null)
            {
                // debug object counts
                Debug.Log("-This Section has " + GetChildren(SelectedSection.ParentEnvironment) +
                          " GameObjects in Environment");
                Debug.Log("-This Section has " + GetChildren(SelectedSection.PickupsParent.gameObject) +
                          " GameObjects in Pickups");
            }
            else
            {
                Debug.LogWarning("You have not selected a LevelSectionCreator OR LevelSection -> Doing nothing");
            }
        }

        // log section Triangles
        if (GUILayout.Button("log Selected Section Triangles"))
        {
            if (SelectedSection != null)
            {
                // debug triangle count
                Debug.Log("This Section has " + GetTriangleCount(SelectedSection.ParentEnvironment) +
                          " Triangles in Environment");
                Debug.Log("This Section has " + GetTriangleCount(SelectedSection.PickupsParent.gameObject) +
                          " Triangles in Pickups");
            }
            else
            {
                Debug.LogWarning("You have not selected a LevelSectionCreator OR LevelSection -> Doing nothing");
            }
        }

        // log section Vertices
        if (GUILayout.Button("log Selected Section Vertices"))
        {
            if (SelectedSection != null)
            {
                // debug triangle count
                Debug.Log("This Section has " + GetVertexCount(SelectedSection.ParentEnvironment) +
                          " Vertices in Environment");
                Debug.Log("This Section has " + GetVertexCount(SelectedSection.PickupsParent.gameObject) + " Vertices in Pickups");
            }
            else
            {
                Debug.LogWarning("You have not selected a LevelSectionCreator OR LevelSection -> Doing nothing");
            }
        }

        // log section Pickups
        if (GUILayout.Button("log Selected Section Pickups"))
        {
            if (SelectedSection != null)
            {
                // debug pickup count
                Debug.Log("-This Section has " + GetPickupCount(SelectedSection.PickupsParent.gameObject) + " Pickups");
            }
            else
            {
                Debug.LogWarning("You have not selected a LevelSectionCreator OR LevelSection -> Doing nothing");
            }
        }
    }




    private int GetChildren(GameObject obj)
    {
        int count = 0;

        for (int i = 0; i < obj.transform.childCount; i++)
        {
            count++;
            Counter(obj.transform.GetChild(i).gameObject, ref count);
        }

        return count;
    }
    private void Counter(GameObject currentObj, ref int count)
    {
        for (int i = 0; i < currentObj.transform.childCount; i++)
        {
            count++;
            Counter(currentObj.transform.GetChild(i).gameObject, ref count);
        }
    }
    private int GetVertexCount(GameObject obj)
    {
        int count = 0;
        MeshFilter[] meshFilters;

        meshFilters = obj.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            count += meshFilters[i].sharedMesh.vertexCount;
            //Debug.Log("this is vertex count of " + meshFilters[i].gameObject.name + " == " + meshFilters[i].sharedMesh.vertexCount);
        }

        return count;
    }
    private int GetTriangleCount(GameObject obj)
    {
        int count = 0;
        MeshFilter[] meshFilters;

        meshFilters = obj.GetComponentsInChildren<MeshFilter>(true);
        for (int i = 0; i < meshFilters.Length; i++)
        {
            int triangleCalc = meshFilters[i].sharedMesh.triangles.Length / 3;
            count += triangleCalc;
            //Debug.Log("this is the triangle count of " + meshFilters[i].gameObject.name + " == " + triangleCalc);
        }

        return count;
    }
    private int GetPickupCount(GameObject obj)
    {
        int count = 0;
        PickUp[] pickups;

        pickups = obj.GetComponentsInChildren<PickUp>(true);
        count += pickups.Length;

        return count;
    }
}