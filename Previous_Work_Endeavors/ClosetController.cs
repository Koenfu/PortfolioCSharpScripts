using System.Collections;
using System.Collections.Generic;
using UnityCore.Audio;
using UnityCore.Menus;
using UnityEngine;
using UnityEngine.UI;

public class ClosetController : MonoBehaviour
{
    public delegate void ClosetControllerDelegate(ButtonClosetOpenSpecificPieces buttonSkinpieceType);
    public event ClosetControllerDelegate OnSkinpieceUnlocked;

    public static ClosetController Instance { get; private set; }

    [Header("Closet References")]
    public List<Page> PagesWithinCloset = new List<Page>();
    public List<ButtonClosetOpenSpecificPieces> ButtonsClosetPagers = new List<ButtonClosetOpenSpecificPieces>();
    public ButtonClosetSelect ButtonCloset;

    public LayerMask LayersToCastOn;

    [HideInInspector]
    public SkinPieceElement CurrentlyHeldSkinPiece;
    [HideInInspector]
    public PageType PageTypeOpenedInClosetLastTime;
    [HideInInspector]
    public GameObject CurrentlyHeldObject;
    [HideInInspector]
    public Animation AnimationSpawnedObject;

    [Header("Notification stuff")]
    public List<ButtonSkinPiece> ButtonsWithNotifications = new List<ButtonSkinPiece>();
    public List<ButtonClosetOpenSpecificPieces> ButtonsClosetPagerWithNotifs = new List<ButtonClosetOpenSpecificPieces>();

    private float _closetPageArmCounter, _closetPageLegCounter, _closetPageFootCounter;

    [HideInInspector]
    public bool ActivatedFollowMouse;

    private Vector3 _mousePosition;
    private Vector3 _mouseWorldPosXY;
    private Vector3 _mouseWorldPositionXYZ;

    private RaycastHit _hit;

    // below is for chugging things in the closet ...(am i sure bout this?)
    [Header("Refs for throwing objects on UI page")]
    public GameObject PanelToInstantiateClosetImagesOn;
    [SerializeField] private GameObject _panelInstantiatedUI;
    [SerializeField] private GameObject _emptyGameObject;

    float _speed;
    float _arcHeight;
    float _stepScale;
    float _progress;

    GameObject _objectToMove;
    Vector2 _startPos, _endPos;


    private void Awake()
    {
        // Singleton 
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }
    private void Start()
    {
        this.enabled = false;
    }
    private void Update()
    {
        FollowMouseLogic();
    }



    // logic for having object follow the mouse
    private void FollowMouseLogic()
    {
        if (Input.GetMouseButtonUp(0))
        {
            LetGoOfMouse();
        }
        else if (ActivatedFollowMouse == true)
        {
            FollowMouseCalculations();
        }
    }
    private void LetGoOfMouse()
    {
        ActivatedFollowMouse = false;

        // check whether I'm currently in the field where letting go would equip the piece
        SkinsMouseController.Instance.EquipSkinPiece(CurrentlyHeldSkinPiece);

        // remove the object 
        Destroy(CurrentlyHeldObject);
        CurrentlyHeldSkinPiece.Data.MyBodyType = Type_Body.None;
        CurrentlyHeldSkinPiece.Data.MySkinType = Type_Skin.None;

        this.enabled = false;
    }
    private void FollowMouseCalculations()
    {
        _mousePosition = Input.mousePosition;
        _mouseWorldPosXY = Camera.main.ScreenToWorldPoint(_mousePosition);

        CurrentlyHeldObject.transform.position = _mouseWorldPosXY;

        if (_mouseWorldPosXY.y > 0)
        {
            if (Physics.Raycast(CurrentlyHeldObject.transform.position, Camera.main.transform.forward, out _hit, Mathf.Infinity, LayersToCastOn))
            {
                _mouseWorldPositionXYZ = _hit.point;
                CurrentlyHeldObject.transform.position = _mouseWorldPositionXYZ;
            }
        }
        else
        {
            if (Physics.Raycast(CurrentlyHeldObject.transform.position, -Camera.main.transform.forward, out _hit, Mathf.Infinity, LayersToCastOn))
            {
                _mouseWorldPositionXYZ = _hit.point;
                CurrentlyHeldObject.transform.position = _mouseWorldPositionXYZ;
            }
        }
    }




    public void ClickedSkinPieceButton(GameObject objectToSpawn, SkinPieceElement skinPieceElement, Vector3 spawnPosition)
    {
        CurrentlyHeldObject = Instantiate(objectToSpawn, PanelToInstantiateClosetImagesOn.transform);
        CurrentlyHeldObject.transform.position = spawnPosition;

        CurrentlyHeldSkinPiece.Data.MyBodyType = skinPieceElement.Data.MyBodyType; 
        CurrentlyHeldSkinPiece.Data.MySkinType = skinPieceElement.Data.MySkinType;
        CurrentlyHeldSkinPiece.HidesSirMouseGeometry = skinPieceElement.HidesSirMouseGeometry;
        CurrentlyHeldSkinPiece.ScoreValue = skinPieceElement.ScoreValue;

        ActivatedFollowMouse = true;

        this.enabled = true;
    }
    public void ClickedSkinPiecePageButton(PageType turnToThisPage)
    {
        foreach (Page page in PagesWithinCloset)
        {
            if (page.Type != turnToThisPage)
            {
                PageController.Instance.TurnPageOff(page.Type);
            }
        }

        PageController.Instance.TurnPageOn(turnToThisPage);

        PageTypeOpenedInClosetLastTime = turnToThisPage;
    }
    public void OpenCloset(PageType typeToOpen)
    {
        // turn off all other pages, except for the closet
        PageController.Instance.TurnAllPagesOffExcept(typeToOpen);

        // open up the last page that was opened within the closet
        if (PageTypeOpenedInClosetLastTime == PageType.None)
        {
            PageController.Instance.TurnPageOn(PageType.ClosetHats);
        }
        else
        {
            PageController.Instance.TurnPageOn(PageTypeOpenedInClosetLastTime);
        }

        // update images
        PageController.Instance.OpenClosetImage(true);
        PageController.Instance.OpenBagImage(false);

        // turn on the UI player things
        SkinsMouseController.Instance.ClosetWrapInsideCamera.gameObject.SetActive(true);

        // activate other camera with overlay rig
        PageController.Instance.CameraUI_Backpack_Closet.enabled = true;
    }
    public void CloseCloset()
    {
        // close closet page
        PageController.Instance.TurnPageOff(PageType.BackpackCloset);

        // update ui images
        PageController.Instance.OpenClosetImage(false);
        PageController.Instance.OpenBagImage(false);

        // this still needed ?
        SkinsMouseController.Instance.ClosetWrapInsideCamera.gameObject.SetActive(false);
        PageController.Instance.CameraUI_Backpack_Closet.enabled = false;
    }

    // called on GiveReward() in RewardController
    public void AddNotificationToList(ButtonSkinPiece buttonSkinPiece)
    {
        bool foundNotification = false;

        if (ButtonsWithNotifications.Contains(buttonSkinPiece) == false)
        {
            if (buttonSkinPiece.HasBeenNotified == false)
            {
                ButtonsWithNotifications.Add(buttonSkinPiece);
                foundNotification = true;
            }          
        }

        if (foundNotification == true)
        {
            PageController.Instance.ButtonBackpackSuper.IhaveNotificationsLeftCloset = true;
            PageController.Instance.NotifyBackpackSuper();
        }
    }
    public void NotificationActivater()
    {
        for (int i = 0; i< ButtonsWithNotifications.Count; i++) // looping over the list of buttons with notifications 
        {
            // only do the following if List[i] NotifObject is not ON
            if (ButtonsWithNotifications[i].NotificationObject.activeSelf == false)
            {
                // activate notif on button skinpiece
                ButtonsWithNotifications[i].NotificationObject.SetActive(true);
                ButtonsWithNotifications[i].HasBeenNotified = true;

                // figure out what pager has a similar BodyType to the ButtonWithNotif...
                for (int j = 0; j < ButtonsClosetPagers.Count; j++)
                {
                    if (ButtonsWithNotifications[i].MySkinPieceElement.Data.MyBodyType == ButtonsClosetPagers[j].BodyType)
                    {
                        // activate notif on found button
                        ButtonsClosetPagers[j].NotificationObject.SetActive(true);

                        if (ButtonsClosetPagerWithNotifs.Contains(ButtonsClosetPagers[j]) == false)
                        {
                            ButtonsClosetPagerWithNotifs.Add(ButtonsClosetPagers[j]);
                        }
                        ButtonsClosetPagers[j].IHaveButtonsWithNotificationOn = true;
                        ButtonsClosetPagers[j].ButtonsWithNotifsOnOnMyPage.Add(ButtonsWithNotifications[i]);                  

                        OnSkinpieceUnlocked?.Invoke(ButtonsClosetPagers[j]);
                        break;
                    } // below statement should add the RIGHT limbs
                    else if ((ButtonsWithNotifications[i].MySkinPieceElement.Data.MyBodyType == Type_Body.FootRight && ButtonsClosetPagers[j].BodyType == Type_Body.FootLeft) ||
                         (ButtonsWithNotifications[i].MySkinPieceElement.Data.MyBodyType == Type_Body.LegRightLower && ButtonsClosetPagers[j].BodyType == Type_Body.LegLeftLower) ||
                         (ButtonsWithNotifications[i].MySkinPieceElement.Data.MyBodyType == Type_Body.ArmRightLower && ButtonsClosetPagers[j].BodyType == Type_Body.ArmLeftLower))
                    {
                        // activate notif on found button
                        ButtonsClosetPagers[j].NotificationObject.SetActive(true);

                        if (ButtonsClosetPagerWithNotifs.Contains(ButtonsClosetPagers[j]) == false)
                        {
                            ButtonsClosetPagerWithNotifs.Add(ButtonsClosetPagers[j]);
                        }
                        ButtonsClosetPagers[j].IHaveButtonsWithNotificationOn = true;
                        ButtonsClosetPagers[j].ButtonsWithNotifsOnOnMyPage.Add(ButtonsWithNotifications[i]);

                        OnSkinpieceUnlocked?.Invoke(ButtonsClosetPagers[j]);
                        break;
                    }
                }
            }
        }

        // activate notif on closet button
        if (ButtonsWithNotifications.Count > 0)
        {          
            ButtonCloset.NotificationObject.SetActive(true);
            ButtonCloset.IhaveNotificationsReadyInTheCloset = true;
        }
    }

    // called when clicking on a piece 
    public void NotificationRemover(ButtonSkinPiece buttonSkinPiece)
    {
        // only jump into the removing logic if this was no TriedOutYet
        if (buttonSkinPiece.TriedThisOut == false)
        {
            buttonSkinPiece.TriedThisOut = true;
            buttonSkinPiece.NotificationObject.SetActive(false);

            // remove it from the list "skinPiece buttons" ... 
            ButtonsWithNotifications.Remove(buttonSkinPiece);

            // AND some other lists...
            for (int i = 0; i < ButtonsClosetPagerWithNotifs.Count; i++)
            {
                // prior IF will not be true if clicking a "Right" limb, hence the extra ELSE IF

                if (buttonSkinPiece.MySkinPieceElement.Data.MyBodyType == ButtonsClosetPagerWithNotifs[i].BodyType)
                {
                    CascadeNotificationLogic(buttonSkinPiece, i);
                    break;
                }
                else if ((buttonSkinPiece.MySkinPieceElement.Data.MyBodyType == Type_Body.FootRight && ButtonsClosetPagerWithNotifs[i].BodyType == Type_Body.FootLeft) ||
                         (buttonSkinPiece.MySkinPieceElement.Data.MyBodyType == Type_Body.LegRightLower && ButtonsClosetPagerWithNotifs[i].BodyType == Type_Body.LegLeftLower) ||
                         (buttonSkinPiece.MySkinPieceElement.Data.MyBodyType == Type_Body.ArmRightLower && ButtonsClosetPagerWithNotifs[i].BodyType == Type_Body.ArmLeftLower))
                {
                    CascadeNotificationLogic(buttonSkinPiece, i);
                    break;
                }
            }
        }
    }
    private void CascadeNotificationLogic(ButtonSkinPiece buttonSkinPiece, int i)
    {
        ButtonsClosetPagerWithNotifs[i].ButtonsWithNotifsOnOnMyPage.Remove(buttonSkinPiece);

        // check if the ButtonClosetSpecificPieces still has any buttons remaining with an active notif
        if (ButtonsClosetPagerWithNotifs[i].ButtonsWithNotifsOnOnMyPage.Count == 0)
        {
            ButtonsClosetPagerWithNotifs[i].IHaveButtonsWithNotificationOn = false;
            ButtonsClosetPagerWithNotifs[i].NotificationObject.SetActive(false);

            ButtonsClosetPagerWithNotifs.Remove(ButtonsClosetPagerWithNotifs[i]);

            // check if there are any ButtonClosetPagerWithNotifs in the list
            if (ButtonsClosetPagerWithNotifs.Count == 0)
            {
                ButtonCloset.IhaveNotificationsReadyInTheCloset = false;
                ButtonCloset.NotificationObject.SetActive(false);

                PageController.Instance.ButtonBackpackSuper.IhaveNotificationsLeftCloset = false;
                PageController.Instance.NotifyBackpackSuper();
            }
        }
    }


    public IEnumerator SetObjectToFalseAfterDelay(GameObject interactable, GameObject spriteParent)
    {
        yield return new WaitForSeconds(0.25f);

        interactable.SetActive(false);
        spriteParent.SetActive(true);
        interactable.GetComponent<Interactable>().HideBalloonBackpack();
    }
}
