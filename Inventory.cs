using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class Inventory : MonoBehaviour
{
    bool transferedInventory = false; // Dont create blank slots, if we are tranfering the inventory from a transform
    public GameObject slotPrefab;
    public const int numSlots = 6;
    public Item[] items = new Item[numSlots];
    public GameObject[] slots = new GameObject[numSlots];
    public Slot[] slotsSlotObjects = new Slot[numSlots];
    public int selectedSlot = 0;

    public Image firebirdTransformIcon;

    public TextMeshProUGUI firebirdTransformTimer;
    public Image quadDamageIcon;

    public TextMeshProUGUI quadDamageTimer;

    public int quadDamageTimeLeft;

    public int numItemsInInventory = 0;
    public Actor actor;
 
    Weapon weapon;

    Coroutine firebirdTransformBlink;

    public Coroutine firebirdTransformCoroutine;
    Coroutine quadDamageBlink;

    public Coroutine quadDamageCoroutine;
    Coroutine flameThrowerMeterCoroutine;
    public Coroutine firebirdCounterCoroutine;
    public Coroutine activeBombCoroutine;
    bool switchedFirebirdSlot = false; // When the firebird slot is switched this variable is set to true and we need to recalculate the slot number. If we don't we'll get an error on the time left counter because it will be using the old slot number.
    bool switchedFlameThrowerSlotWhileFiring = false; // When the flamethrower slot is switched while firing this variable is set to true and we need to recalculate the slot number. If we don't we'll get an error on the fill meter bar because it will be using the old slot number.
    
    bool switchedActiveBombSlot = false; // When a slot with an active bomb coroutine is switched, this variable is set to true and we need to recalculate the slot number. If we don't we'll get an error on the bomb slot sprite get enable/disable because it will be using the old slot number.
    bool activeBombIsBlinking = false; // Used to know if to recalculate the active bomb slot.
    const int INIT_FIREBIRD_TIME = 60; // The player has 60 seconds to use the firebird pickup.
    public bool hasActiveBomb = false;
    public float activeBombTimePassed = 0;
    const float TIME_TO_BOMB_BLINK = 2.0f;
    const float BOMB_BLINK_DURATION = 0.1f;
    bool canChangeInventory = true;
    public GameObject bombPrefab;

    public int firebirdTimeLeft; // Used for passing the firebird counter coroutine to the new inventory when the player changes forms (firbird -> player or player -> firebird)

    void OnEnable()
    {
        quadDamageIcon = transform.GetChild(1).GetComponent<Image>();
        quadDamageTimer = quadDamageIcon.GetComponent<QuadDamageIcon>().quadDamageTimerText;
        firebirdTransformIcon = transform.GetChild(2).GetComponent<Image>();
        firebirdTransformTimer = firebirdTransformIcon.GetComponent<FirebirdIcon>().firebirdTimerText;
    }
    void Start()
    {
        if(!transferedInventory)
            CreateSlots();
        
        if(actor.actorType == Actor.ActorType.Player)
            weapon = actor.gameObject.GetComponent<Weapon>();
        if(actor.actorType == Actor.ActorType.Firebird)
            EnableFirebirdTransformTimer();
    }

    public void CreateSlots() // When the actor is spawned, create six slots and set slot num 0 (first) to active.
    {
        if (slotPrefab != null)
        {
            for (int i = 0; i < numSlots; i++)
            {
                GameObject newSlot = Instantiate(slotPrefab);
                newSlot.name = "ItemSlot_" + i;
                newSlot.transform.SetParent(gameObject.transform.GetChild(0).transform);
                slots[i] = newSlot;
                //itemImages[i] = newSlot.transform.GetChild(1).GetComponent<Image>();
            }
            Slot selected = slots[selectedSlot].gameObject.GetComponent<Slot>();
            selected.ToggleSlotActive(true);
        }
    }

    public bool AddItem(Item itemToAdd, bool isActiveBomb = false, float activeBombTimeLeft = 0.0f) // There are three ways to add a new item to the inventory. The first way takes the highest priority, the last way takes the lowest priority. First we check to see if item is already in the inventory, if it is we add it to that slot and increase the quantity. If it is not we then check if the selected slot is empty and add it to the selected slot. If the selected slot is not empty, we then find the first empty slot and add it to that slot.
    {
        if(numItemsInInventory == 0) // The inventory is empty, we can safely add the item to the current selected slot, since the item cannot possibly already be in the inventory.
        {
            InsertItem(true,selectedSlot,itemToAdd,true,isActiveBomb,activeBombTimeLeft);
            return true;
        }
        else
        {
            int firstEmpty = -1; // -1 is null essentially
            bool selectedSlotIsEmpty;

            if(items[selectedSlot] == null) // See if the selected slot is empty.
                selectedSlotIsEmpty = true;
            else
                selectedSlotIsEmpty = false;

            for (int i = 0; i < items.Length; i++)
            {
                if(items[i] == null)
                {
                    if(!selectedSlotIsEmpty && firstEmpty == -1) //Find the first empty slot and add the index to the firstEmpty variable
                        firstEmpty = i;
                    continue;
                }
                else if(items[i].itemType.Equals(itemToAdd.itemType)) // First, run through each slot in the inventory and check if the item is already in it. If it is, instead of adding the item to a new slot and having a duplicate, add it to the slot where the item already is in and increase the quantity.
                {
                    InsertItem(false,i,itemToAdd,false,isActiveBomb,activeBombTimeLeft);
                    return true; // We have added the item to the inventory in the current slot, we are finished.
                }
                //else if(!selectedSlotIsEmpty && firstEmpty == -1 && items[i] == null) // Find the first slot that is empty.
                //    firstEmpty = i;
            }
        
            if(selectedSlotIsEmpty)
            {
                InsertItem(true,selectedSlot,itemToAdd,true,isActiveBomb,activeBombTimeLeft);
                return true;
            }
            else if(firstEmpty != -1)
            {
                InsertItem(true,firstEmpty,itemToAdd,false,isActiveBomb,activeBombTimeLeft);
                return true;
            }
        }
        return false; // Unable to add item to the inventory.
    }

    void InsertItem(bool itemIsntAlreadyInInventory, int slotIndex, Item itemToAdd, bool emptySlot, bool isActiveBomb, float activeBombTimeLeft)
    {
        Slot slotAdd = slots[slotIndex].gameObject.GetComponent<Slot>();

        if(itemIsntAlreadyInInventory) //Instantiate the item for a new slot
        {
            items[slotIndex] = Instantiate(itemToAdd);
            items[slotIndex].quantity = itemToAdd.quantity;
            
            slotAdd.itemImage.sprite = itemToAdd.sprite;
            slotAdd.itemImage.enabled = true;
            slotAdd.qtyText.enabled = true;
            slotAdd.qtyText.text = items[slotIndex].quantity.ToString();
            numItemsInInventory++;
        }
        else // Slot already exists, simply increase the quantity of the item.
        {  // Dont increment numitemsininventory because we are not putting a new item in.
            items[slotIndex].quantity += itemToAdd.quantity;
        }

        slotAdd.qtyText.text = items[slotIndex].quantity.ToString();
        HandleSpecialItems(slotAdd,itemToAdd,slotIndex,emptySlot,isActiveBomb,activeBombTimeLeft);
    }

    void HandleSpecialItems(Slot slotAdd, Item itemToAdd, int slotIndex, bool emptySlot, bool isActiveBomb, float activeBombTimeLeft)
    {
        if(!emptySlot)
        {
            if(isActiveBomb)
            {
                    if(flameThrowerMeterCoroutine != null) // If the player picks up an active bomb while the flamethrower is firing and it changes the selected slot, stop the flamethrower.
                        StopFlameThrowerMeter();
                        
                    slots[selectedSlot].GetComponent<Slot>().ToggleSlotActive(false);
                    selectedSlot = slotIndex; // Change the selected slot to the slot with the bomb since an active bomb has been added to the inventory.
                    AddActiveBomb(activeBombTimeLeft,slotAdd);
                    slotAdd.ToggleSlotActive(true);
            }
        }
        else
        {
            if(isActiveBomb)
            {
                AddActiveBomb(activeBombTimeLeft,slotAdd);
            }
        }

        if(itemToAdd.itemType == Item.ItemType.INHALER)
        {
            if(itemToAdd.intLeftList.Count == 0) // If an item is spawned from a box, it does not yet have an item in the intleftlist (This is created when the item is added to the inventory.) Therefore we can safely add it to the items array intleftlist without it creating a duplicate.
            {
                AddTimeLeftToList(true,slotIndex,itemToAdd);
                InstantiateInhalerMeter(slotAdd,itemToAdd);
            }
            else // The item already has entries in the intleftlist (it has been previously dropped by a player), therefore do not call the addtimelefttolist method because the list already exists. If we call the method, it will result in an erroneous duplicate in the list.
            {
                InstantiateInhalerMeter(slotAdd,itemToAdd);
            }
        }
        else if(itemToAdd.itemType == Item.ItemType.PHOENIX)
        {
            if(itemToAdd.intLeftList.Count == 0) // If an item is spawned from a box, it does not yet have an item in the intleftlist (This is created when the item is added to the inventory.) Therefore we can safely add it to the items array intleftlist without it creating a duplicate.
            {
                AddTimeLeftToList(true,slotIndex,itemToAdd);
                EnableFirebirdCounter(items[slotIndex].intLeftList[0]);
            }
            else // The item already has entries in the intleftlist (it has been previously dropped by a player), therefore do not call the addtimelefttolist method because the list already exists. If we call the method, it will result in an erroneous duplicate in the list.
            {
                EnableFirebirdCounter(items[slotIndex].intLeftList[0]);
            }

                //AddTimeLeftToList(true,firstEmpty,itemToAdd);
                //EnableFirebirdCounter(items[firstEmpty].intLeftList[0]);
        }
        else if(itemToAdd.itemType == Item.ItemType.FLAMETHROWER)
        {
            if(itemToAdd.floatLeftList.Count == 0) // If an item is spawned from a box, it does not yet have an item in the intleftlist (This is created when the item is added to the inventory.) Therefore we can safely add it to the items array intleftlist without it creating a duplicate.
            {
                AddTimeLeftToList(false,slotIndex,itemToAdd);
            }
            else
            {
                if(itemToAdd.floatLeftList[0] != 30.0f)
                    InstantiateFlameThrowerMeter(slotAdd,itemToAdd);
            }
        }
    }
    

    public Item RemoveItem()
    {
        if(items[selectedSlot] != null && canChangeInventory)
        {
            if(items[selectedSlot].itemType == Item.ItemType.PHOENIX) // If the dropped item is a phoenix stop the counter coroutine.
            {
                StopCoroutine(firebirdCounterCoroutine);
                firebirdCounterCoroutine = null;
            }

            if(items[selectedSlot].itemType == Item.ItemType.FLAMETHROWER && flameThrowerMeterCoroutine != null) // If the dropped item is a flamethrower and it is firing, stop the meter coroutine, and the firing method in the weapon class.
            {
                weapon.flameThrowerActive = false;
                StopCoroutine(flameThrowerMeterCoroutine);
                flameThrowerMeterCoroutine = null;
            }

            Item newItem = Instantiate(items[selectedSlot]); // The player is dropping an item, so instantiate a copy and pass it to the calling method.
            
            items[selectedSlot] = null;
            DisableSlot(selectedSlot, false);
            numItemsInInventory--;

            return newItem;
        }
        else // No item in the selected slot, return null.
            return null;
    }

    public Bomb RemoveActiveBomb()
    {
        GameObject bombObject = Instantiate(bombPrefab);
        Bomb activeBomb = bombObject.GetComponent<Bomb>();
        activeBomb.timePassed = activeBombTimePassed;
        StopCoroutine(activeBombCoroutine);
        ResetActiveBombCoroutine(true);
    
        return activeBomb;
    }

    public void MoveWeaponUp()
    {
        print("debugmoveweaponup");
        if(items[selectedSlot] != null) // If the slot selected is null do nothing.
        {
            if(items[selectedSlot].itemType == Item.ItemType.PHOENIX) // Set switchedslot boolean for firebird true, so the counter updates to the new slot.
                switchedFirebirdSlot = true;

            else if(items[selectedSlot].itemType == Item.ItemType.FLAMETHROWER && flameThrowerMeterCoroutine != null) // Set switchedslot boolean for flamethrower to true, so the meter updates to the new slot.
            {
                switchedFlameThrowerSlotWhileFiring = true;
            }
            else if(items[selectedSlot].itemType == Item.ItemType.BOMB && hasActiveBomb) // Set switchedslot boolean for active true, so the blinking slot updates to the new slot.
            {
                switchedActiveBombSlot = true;
            }

            if(selectedSlot != slots.Length - 1)
            {
                if(items[selectedSlot + 1] == null) // The next slot is null move the item up a lot and make the current slot empty.
                {
                    Slot currentSlot = slots[selectedSlot].gameObject.GetComponent<Slot>();
                    
                    items[selectedSlot + 1] = items[selectedSlot];
                    EnableSlot(currentSlot, selectedSlot + 1);

                    items[selectedSlot] = null;
                    DisableSlot(selectedSlot);

                    selectedSlot++;
                }
                else
                {
                    if(items[selectedSlot + 1].itemType == Item.ItemType.PHOENIX) // We need to check if the next slot has a firebird powerup in it and if it does, set the bool for it to true since it is also changing the slot.
                            switchedFirebirdSlot = true;
                    
                    else if(items[selectedSlot + 1].itemType == Item.ItemType.BOMB && hasActiveBomb)  // We need to check if the next slot has an active bomb blinking in it and if it does, set the bool for it to true since it is also changing the slot.
                    {
                        switchedActiveBombSlot = true;
                    }

                    Slot currentSlot = Instantiate(slots[selectedSlot].gameObject.GetComponent<Slot>());
                    Slot newSlot = Instantiate(slots[selectedSlot + 1].gameObject.GetComponent<Slot>());
                    Item tempItem = items[selectedSlot + 1];

                    SwitchSlot(selectedSlot, selectedSlot + 1, currentSlot, newSlot);
                    items[selectedSlot + 1] = items[selectedSlot];
                    items[selectedSlot] = tempItem;

                    //if(activeBombIsBlinking)
                    //    CheckAllNonBombSlotImagesAreEnableAfterSwap();

                    selectedSlot++;  
                }   
            }
            else
            {
                if(items[0] == null) // The next slot is null move the item up a lot and make the current slot empty.
                {
                    Slot currentSlot = slots[selectedSlot].gameObject.GetComponent<Slot>();
                    
                    items[0] = items[selectedSlot];
                    EnableSlot(currentSlot, 0);

                    items[selectedSlot] = null;
                    DisableSlot(selectedSlot);

                    selectedSlot = 0;
                }
                else
                {
                    if(items[0].itemType == Item.ItemType.PHOENIX) // We need to check if the next slot has a firebird powerup in it and if it does, set the bool for it to true since it is also changing the slot.
                        switchedFirebirdSlot = true;
                    
                    else if(items[0].itemType == Item.ItemType.BOMB && hasActiveBomb)  // We need to check if the next slot has an active bomb blinking in it and if it does, set the bool for it to true since it is also changing the slot.
                    {
                        switchedActiveBombSlot = true;
                    }

                    Slot currentSlot = Instantiate(slots[selectedSlot].gameObject.GetComponent<Slot>());
                    Slot newSlot = Instantiate(slots[0].gameObject.GetComponent<Slot>());
                    Item tempItem = items[0];

                    SwitchSlot(selectedSlot, 0, currentSlot, newSlot);
                    items[0] = items[selectedSlot];
                    items[selectedSlot] = tempItem;

                    //if(activeBombIsBlinking)
                    //    CheckAllNonBombSlotImagesAreEnableAfterSwap();

                    selectedSlot = 0;              
                }
            }
        }
    }

    public void MoveWeaponDown()
    {
        print("debugmovedown");
        if(items[selectedSlot] != null) // If the slot selected is null do nothing.
        {
            print(items[selectedSlot].itemType);

            if(items[selectedSlot].itemType == Item.ItemType.PHOENIX)
            {
                print("switchedfirebird");
                switchedFirebirdSlot = true;
            }

            else if(items[selectedSlot].itemType == Item.ItemType.FLAMETHROWER && flameThrowerMeterCoroutine != null)
            {
                switchedFlameThrowerSlotWhileFiring = true;
                // We need to check if the previous slot has a firebird powerup in it and if it does, set the bool for it to true since it is also changing the slot.
            }

            else if(items[selectedSlot].itemType == Item.ItemType.BOMB && hasActiveBomb) // Set switchedslot boolean for active true, so the blinking slot updates to the new slot.
            {
                switchedActiveBombSlot = true;
            }

            if(selectedSlot != 0)
            {
                if(items[selectedSlot - 1] == null) // The next slot is null move the item up a slot and make the current slot empty.
                {
                    Slot currentSlot = slots[selectedSlot].gameObject.GetComponent<Slot>();
                    
                    items[selectedSlot - 1] = items[selectedSlot];
                    EnableSlot(currentSlot, selectedSlot - 1);

                    items[selectedSlot] = null;
                    DisableSlot(selectedSlot);

                    selectedSlot--;
                }

                else
                {
                    print("moved down not at the end");
                    if(items[selectedSlot - 1].itemType == Item.ItemType.PHOENIX)
                        switchedFirebirdSlot = true;
                    
                    else if(items[selectedSlot - 1].itemType == Item.ItemType.BOMB && hasActiveBomb) // Set switchedslot boolean for active true, so the blinking slot updates to the new slot.
                    {
                        switchedActiveBombSlot = true;
                    }

                    Slot currentSlot = Instantiate(slots[selectedSlot].gameObject.GetComponent<Slot>());
                    Slot newSlot = Instantiate(slots[selectedSlot - 1].gameObject.GetComponent<Slot>());
                    Item tempItem = items[selectedSlot - 1];

                    SwitchSlot(selectedSlot, selectedSlot - 1, currentSlot, newSlot);
                    items[selectedSlot - 1] = items[selectedSlot];
                    items[selectedSlot] = tempItem;

                    //if(activeBombIsBlinking)
                    //    CheckAllNonBombSlotImagesAreEnableAfterSwap();

                    selectedSlot--;  
                }
            }
            else
            {
                if(items[items.Length - 1] == null) // The next slot is null move the item up a lot and make the current slot empty.
                {
                    Slot currentSlot = slots[selectedSlot].gameObject.GetComponent<Slot>();
                    
                    items[items.Length - 1] = items[selectedSlot];
                    EnableSlot(currentSlot, slots.Length - 1);

                    items[selectedSlot] = null;
                    DisableSlot(selectedSlot);

                    selectedSlot = slots.Length - 1;
                }
                else
                {
                    if(items[items.Length - 1].itemType == Item.ItemType.PHOENIX)
                        switchedFirebirdSlot = true;
                    
                    else if(items[items.Length - 1].itemType == Item.ItemType.BOMB && hasActiveBomb) // Set switchedslot boolean for active true, so the blinking slot updates to the new slot.
                    {
                        switchedActiveBombSlot = true;
                    }

                    Slot currentSlot = Instantiate(slots[selectedSlot].gameObject.GetComponent<Slot>());
                    Slot newSlot = Instantiate(slots[slots.Length - 1].gameObject.GetComponent<Slot>());
                    Item tempItem = items[items.Length - 1];

                    SwitchSlot(selectedSlot, slots.Length - 1, currentSlot, newSlot);
                    items[items.Length - 1] = items[selectedSlot];
                    items[selectedSlot] = tempItem;

                    //if(activeBombIsBlinking)
                    //    CheckAllNonBombSlotImagesAreEnableAfterSwap();

                    selectedSlot = items.Length - 1;              
                }
            }
        }
    }

    void SwitchSlot(int num1, int num2, Slot newSlot1, Slot newSlot2)
    {
        Slot slotnum1 = slots[num1].GetComponent<Slot>();
        Slot slotnum2 = slots[num2].GetComponent<Slot>();
        
        slotnum1.itemImageObject = newSlot2.itemImageObject;
        slotnum1.itemImage.sprite = newSlot2.itemImage.sprite;
        slotnum1.qtyText.text = newSlot2.qtyText.text;

        if(newSlot2.floatMeterFill.enabled)
        {
            slotnum1.floatMeterFill.enabled = true;
            slotnum1.floatMeterFill.fillAmount = newSlot2.floatMeterFill.fillAmount;
        }
        else
        {
            slotnum1.floatMeterFill.enabled = false;
            slotnum1.floatMeterFill.fillAmount = 1.0f;
        }

        if(newSlot2.firebirdTimeLeft.enabled)
        {
            slotnum1.firebirdTimeLeft.enabled = true;
            slotnum1.firebirdTimeLeft.text = newSlot2.firebirdTimeLeft.text;
        }
        else
        {
            slotnum1.firebirdTimeLeft.enabled = false;
            slotnum1.firebirdTimeLeft.text = newSlot2.firebirdTimeLeft.text;
        }
    
        slotnum2.itemImageObject = newSlot1.itemImageObject;
        slotnum2.itemImage.sprite = newSlot1.itemImage.sprite;
        slotnum2.qtyText.text = newSlot1.qtyText.text;

        if(newSlot1.floatMeterFill.enabled)
        {
            slotnum2.floatMeterFill.enabled = true;
            slotnum2.floatMeterFill.fillAmount = newSlot1.floatMeterFill.fillAmount;
        }
        else
        {
            slotnum2.floatMeterFill.enabled = false;
            slotnum2.floatMeterFill.fillAmount = 1.0f;
        }

        if(newSlot1.firebirdTimeLeft.enabled)
        {
            slotnum2.firebirdTimeLeft.enabled = true;
            slotnum2.firebirdTimeLeft.text = newSlot1.firebirdTimeLeft.text;
        }
        else
        {
            slotnum2.firebirdTimeLeft.enabled = false;
            slotnum2.firebirdTimeLeft.text = newSlot2.firebirdTimeLeft.text;
        }

        slotnum1.ToggleSlotActive(false);
        slotnum2.ToggleSlotActive(true);
    }

    void DisableSlot(int num, bool turnOffSlot = true)
    {
        print("debug disabled slot called");
        Slot newSlot = slots[num].GetComponent<Slot>();
        newSlot.itemImage.enabled = false;
        newSlot.qtyText.enabled = false;

        if(newSlot.floatMeterFill.enabled)
        {
            newSlot.floatMeterFill.enabled = false;
            newSlot.floatMeterFill.GetComponent<Image>().fillAmount = 1.0f;
        }
        if(newSlot.firebirdTimeLeft.enabled)
        {
            newSlot.firebirdTimeLeft.enabled = false;
            newSlot.firebirdTimeLeft.text = INIT_FIREBIRD_TIME.ToString();
        }

        if(turnOffSlot)
            newSlot.ToggleSlotActive(false);
    }

    void EnableSlot(Slot currentSlot, int num)
    {
        Slot newSlot = slots[num].GetComponent<Slot>();

        newSlot.itemImage.sprite = currentSlot.itemImage.sprite;
        newSlot.qtyText.text = currentSlot.qtyText.text;
        newSlot.itemImage.enabled = true;
        newSlot.qtyText.enabled = true;

        if(currentSlot.floatMeterFill.enabled) // Check if the meter fill is enabled and if it is swap it to the new slot.
        {
            newSlot.floatMeterFill.enabled = true;
            newSlot.floatMeterFill.fillAmount = currentSlot.floatMeterFill.fillAmount;
        }

        if(currentSlot.firebirdTimeLeft.enabled)
        {
            newSlot.firebirdTimeLeft.enabled = true;
            newSlot.firebirdTimeLeft.text = currentSlot.firebirdTimeLeft.text;
        }

        newSlot.ToggleSlotActive(true);
    }



    public void SelectPreviousWeapon()
    {
        if(canChangeInventory)
        {
        if(items[selectedSlot] != null)
        {
            if(weapon != null && items[selectedSlot].itemType == Item.ItemType.FLAMETHROWER) // If you change weapons from the flamethrower, stop the coroutine for the meter and stop the flamethrower firing coroutine for the player. We check if weapon is null because the firebird does not have a Weapon class (it instead has firebirdweapon).
            {
                weapon.firingFlamethrowerBeforeSpinJump = false;
                StopFlameThrowerMeter();
            }
        }

        Slot selected = slots[selectedSlot].gameObject.GetComponent<Slot>();
        selected.ToggleSlotActive(false);
        if (selectedSlot == 0)
            selectedSlot = slots.Length - 1;
        else
            selectedSlot--;
        selected = slots[selectedSlot].gameObject.GetComponent<Slot>();
        selected.ToggleSlotActive(true);
        }
    }
    
    public void SelectNextWeapon()
    {
        if(canChangeInventory)
        {
        if(items[selectedSlot] != null)
        {
            if(weapon != null && items[selectedSlot].itemType == Item.ItemType.FLAMETHROWER) // If you change weapons from the flamethrower, stop the coroutine for the meter and stop the flamethrower firing coroutine for the player. We check if weapon is null because the firebird does not have a Weapon class (it instead has firebirdweapon).
            {   
                weapon.firingFlamethrowerBeforeSpinJump = false;
                StopFlameThrowerMeter();
            }
        }
        
        Slot selected = slots[selectedSlot].gameObject.GetComponent<Slot>();
        selected.ToggleSlotActive(false);
        if (selectedSlot == slots.Length - 1)
            selectedSlot = 0;

        else
            selectedSlot++;

        selected = slots[selectedSlot].gameObject.GetComponent<Slot>();
        selected.ToggleSlotActive(true);
        }
    }

    public void SelectSlotNumber(int num) // Called when the player selects a weapon using the numeric keys.
    {
        if(canChangeInventory)
        {
        if(selectedSlot != num - 1) //Only update the slot if it not the one currently selected.
        {
            if(items[selectedSlot] != null)
            {
                if(weapon != null && items[selectedSlot].itemType == Item.ItemType.FLAMETHROWER) // If you change weapons from the flamethrower, stop the coroutine for the meter and stop the flamethrower firing coroutine for the player. We check if weapon is null because the firebird does not have a Weapon class (it instead has firebirdweapon).
                {
                    weapon.firingFlamethrowerBeforeSpinJump = false;
                    StopFlameThrowerMeter();
                }
            }

            Slot selected = slots[selectedSlot].gameObject.GetComponent<Slot>();
            selected.ToggleSlotActive(false);

            selected = slots[num - 1].gameObject.GetComponent<Slot>();
            selected.ToggleSlotActive(true);
            selectedSlot = num - 1;
        }
        }
    }

    public Item ReturnSelectedWeapon()
    {
        if(items[selectedSlot] != null)
            return items[selectedSlot];
        else
            return null;
    }

    public void DecrementAmmoCount(int num)
    {
        print("ammo decremented");
        Slot currentSlot = slots[num].GetComponent<Slot>();

        int ammo = Int32.Parse(currentSlot.qtyText.text);
        items[num].quantity--;
        ammo--;
        currentSlot.qtyText.text = ammo.ToString();

        if(items[num].itemType == Item.ItemType.PHOENIX || items[num].itemType == Item.ItemType.INHALER) //Pop the first int from the list if the object is a phoenix or inhaler.
        {
            items[num].intLeftList.RemoveAt(0);

            if(items[num].itemType == Item.ItemType.PHOENIX) // If the player uses a firebird transform, stop the counter coroutine (to prevent a null error.) If there is another firebird in the slot, restart the coroutine.
            {
                if(firebirdCounterCoroutine != null) 
                {
                    StopCoroutine(firebirdCounterCoroutine);
                    firebirdCounterCoroutine = null;

                    if(items[num].quantity != 0)
                    {
                        EnableFirebirdCounter(items[num].intLeftList[0]);
                    }
                }
            }
        }
        else if(items[num].itemType == Item.ItemType.FLAMETHROWER) //Pop the first float from the list if the object is a flamethrower.
            items[num].floatLeftList.RemoveAt(0);
      

        if(ammo <= 0)
        {
            items[num] = null;
            numItemsInInventory--;
            DisableSlot(num,false);
        }
    }

    public void HitInhaler()
    {
        items[selectedSlot].intLeft--;
        items[selectedSlot].intLeftList[0]--;

        Slot selected = slots[selectedSlot].GetComponent<Slot>();

        selected.floatMeterFill.fillAmount = (float)items[selectedSlot].intLeftList[0] / 6;

        //if(items[selectedSlot].intLeft <= 0)
        if(items[selectedSlot].intLeftList[0] <= 0)
        {
            DecrementAmmoCount(selectedSlot);

            if(items[selectedSlot] == null)
            {
                selected.floatMeterFill.enabled = false; // Reset
                selected.floatMeterFill.fillAmount = 1.0f;
            }
            else
            {   
                items[selectedSlot].intLeft = 6;
                selected.floatMeterFill.fillAmount = (float)items[selectedSlot].intLeftList[0] / 6;
            }
        }
    }

    public void CrossOutSlots(bool enableCrossOut)
    {
        for(int i = 0; i < slots.Length; i++)
        {
            slots[i].GetComponent<Slot>().PutCrossOutOnSlot(enableCrossOut);
        }
    }

    public void EnableFirebirdCounter(int start)
    {
        if(firebirdCounterCoroutine == null)
            firebirdCounterCoroutine = StartCoroutine(FirebirdCoroutine(start));
    }
    IEnumerator FirebirdCoroutine(int start)
    {
        Slot firebirdSlot = slots[selectedSlot].GetComponent<Slot>(); // Set to null, since C# doesn't recognize that the firebird object has to be in the slots array
        int firebirdSlotNum = -1;

        FindFireBirdSlotAndSlotNumber(ref firebirdSlot, ref firebirdSlotNum);

        firebirdSlot.firebirdTimeLeft.enabled = true;
        firebirdSlot.firebirdTimeLeft.text = start.ToString();

        while(Int32.Parse(firebirdSlot.firebirdTimeLeft.text) > 0)
        {
            if(switchedFirebirdSlot)
            {
                print("debug switching firebird slot first");
                FindFireBirdSlotAndSlotNumber(ref firebirdSlot, ref firebirdSlotNum);
                switchedFirebirdSlot = false;
            }

            int timeLeft = Int32.Parse(firebirdSlot.firebirdTimeLeft.text);
            firebirdTimeLeft = timeLeft;
            yield return new WaitForSeconds(1.0f);

            if(switchedFirebirdSlot)
            {
                print("debug switching firebird slot second");
                FindFireBirdSlotAndSlotNumber(ref firebirdSlot, ref firebirdSlotNum);
                switchedFirebirdSlot = false;
                timeLeft = Int32.Parse(firebirdSlot.firebirdTimeLeft.text);
            }

            timeLeft--;
            items[firebirdSlotNum].intLeft = timeLeft;
            items[firebirdSlotNum].intLeftList[0] = timeLeft;
            firebirdSlot.firebirdTimeLeft.text = timeLeft.ToString();
        }

        firebirdCounterCoroutine = null;
        DecrementAmmoCount(firebirdSlotNum);

        if(items[firebirdSlotNum] == null)
        {
            firebirdSlot.firebirdTimeLeft.enabled = false;
            firebirdSlot.firebirdTimeLeft.text = INIT_FIREBIRD_TIME.ToString();
            //firebirdCounterCoroutine = null;
        }

        else
        {
            firebirdCounterCoroutine = null;
            EnableFirebirdCounter(items[firebirdSlotNum].intLeftList[0]);
        }
    }

    void FindFireBirdSlotAndSlotNumber(ref Slot firebirdSlot, ref int firebirdSlotNum)
    {
        print("debug searching for slot");

        for(int i = 0; i < items.Length; i++)
        {
            if(items[i] != null) // check for null first before proceeding. (Otherwise null reference error if the slot is empty.)
            {
                print("i"+i);
                print(items[i].itemType);
                if(items[i].itemType == Item.ItemType.PHOENIX)
                {
                    firebirdSlot = slots[i].GetComponent<Slot>();
                    firebirdSlotNum = i;
                    print("new firebird slot num:"+i);
                }
            }
        }
    }

    void RecalculateFlameThrowerSlotNumber(ref Slot flameThrowerSlot, ref int num) // If we switch the flamethrower slot while the flamethrower is firing, we must recalculate the slot number for the coroutine while loop. Use a refernce so it's changed in the calling method.
    {
        for(int i = 0; i < items.Length; i++)
        {
            if(items[i] != null) // check for null first before proceeding. (Otherwise null reference error if the slot is empty.)
            {
                if(items[i].itemType == Item.ItemType.FLAMETHROWER)
                {
                    flameThrowerSlot = slots[i].GetComponent<Slot>();
                    num = i;
                }
            }
        }
    }


    public void EnableFirebirdTransformTimer()
    {
        if(firebirdTransformCoroutine != null)
            StopCoroutine(firebirdTransformCoroutine);
        if(firebirdTransformBlink != null)
        {
            StopCoroutine(firebirdTransformBlink);
            firebirdTransformBlink = null;
        }
        firebirdTransformCoroutine = StartCoroutine(FirebirdTransformCoroutine());
    }

    IEnumerator FirebirdTransformCoroutine()
    {
        firebirdTransformIcon.enabled = true;
        firebirdTransformTimer.enabled = true;
        int timeLeftToTransform = 90;

        while(timeLeftToTransform > 0)
        {
            if(timeLeftToTransform <= 5 && firebirdTransformBlink == null)
                firebirdTransformBlink = StartCoroutine(BlinkIcon(timeLeftToTransform,false));

            firebirdTransformTimer.text = timeLeftToTransform.ToString();
            yield return new WaitForSeconds(1.0f);
            timeLeftToTransform--;
        }

        firebirdTransformIcon.enabled = false;
        firebirdTransformTimer.enabled = false;
        firebirdTransformCoroutine = null;
        actor.GetComponent<Firebird>().TransformBackIntoPlayer();
    }

    
    public void EnableQuadDamage(int start)
    {
        if(quadDamageCoroutine != null)
            StopCoroutine(quadDamageCoroutine);
        if(quadDamageBlink != null)
        {
            StopCoroutine(quadDamageBlink);
            quadDamageBlink = null;
        }
        quadDamageCoroutine = StartCoroutine(QuadDamageCoroutine(start));
    }

    IEnumerator QuadDamageCoroutine(int start)
    {
        actor.damageFactor = 3.0f;
        quadDamageIcon.enabled = true;
        quadDamageTimer.enabled = true;
        quadDamageTimeLeft = start;

        while(quadDamageTimeLeft > 0)
        {
            if(quadDamageTimeLeft <= 5 && quadDamageBlink == null)
                quadDamageBlink = StartCoroutine(BlinkIcon(quadDamageTimeLeft,true));

            quadDamageTimer.text = quadDamageTimeLeft.ToString();
            yield return new WaitForSeconds(1.0f);
            quadDamageTimeLeft--;
        }

        actor.damageFactor = 1.0f;
        quadDamageIcon.enabled = false;
        quadDamageTimer.enabled = false;
        quadDamageCoroutine = null;
    }

    IEnumerator BlinkIcon(int start, bool blinkQuadDamage)
    {
        for(int i = 0; i < start * 10 - 1; i++)
        {
            if(i % 2 == 0) // Make the bomb blink, because it is about to explode.
            {
                if(blinkQuadDamage)
                    quadDamageIcon.enabled = false;
                else
                    firebirdTransformIcon.enabled = false;
            }
            else
            {
                if(blinkQuadDamage)
                    quadDamageIcon.enabled = true;
                else
                    firebirdTransformIcon.enabled = true;

            }

            yield return new WaitForSeconds(0.1f);
        }
        if(blinkQuadDamage)
            quadDamageBlink = null;
        else
            firebirdTransformBlink = null;
        
    }

    public void StopFlameThrowerMeter()
    {
        if(weapon.flameThrowerActive) // Check to make sure the flamethrower is firing in the weapon class and stop it if it is. Only called when we switch weapons while the flamethrower is active. (FlameThrowerActive is set to false when the player stops firing the flamethrower in the Weapon class (By hitting the fire button again).)
        {
            weapon.PauseFlameThrower();
        }

        if(flameThrowerMeterCoroutine != null)
        {
            StopCoroutine(flameThrowerMeterCoroutine);
            flameThrowerMeterCoroutine = null;
        }
    }
    public void StartFlameThrowerMeter()
    {
        Slot activeSlot = slots[selectedSlot].GetComponent<Slot>();
        if(flameThrowerMeterCoroutine == null)
        {
            flameThrowerMeterCoroutine = StartCoroutine(FlameThrowerMeter(activeSlot,selectedSlot));
        }
    }
    IEnumerator FlameThrowerMeter(Slot activeSlot, int num)
    {
        activeSlot.floatMeterFill.enabled = true;
        Image meterFillImage = activeSlot.floatMeterFill.GetComponent<Image>();

        print("num"+num);
        while(items[num].floatLeftList[0] > 0)
        {
            if(switchedFlameThrowerSlotWhileFiring)
            {
                RecalculateFlameThrowerSlotNumber(ref activeSlot, ref num);
                meterFillImage = activeSlot.floatMeterFill.GetComponent<Image>();
                switchedFlameThrowerSlotWhileFiring = false;
            }

            items[num].floatleft -= Time.deltaTime;
            items[num].floatLeftList[0] -= Time.deltaTime;
            meterFillImage.fillAmount = items[num].floatLeftList[0] / 30.0f;
            yield return null;

            if(switchedFlameThrowerSlotWhileFiring)
            {
                RecalculateFlameThrowerSlotNumber(ref activeSlot, ref num);
                meterFillImage = activeSlot.floatMeterFill.GetComponent<Image>();
                switchedFlameThrowerSlotWhileFiring = false;
            }
        }

        DecrementAmmoCount(num);

        if(items[num] == null)
        {
            activeSlot.floatMeterFill.enabled = false; 
            meterFillImage.fillAmount = 1.0f;
        }
        else
        {
            if(items[num].floatLeftList[0] == 30.0f)
            {
                activeSlot.floatMeterFill.enabled = false; 
                meterFillImage.fillAmount = 1.0f;
            }
            else
            {
                meterFillImage.fillAmount = items[num].floatLeftList[0] / 30.0f;
            }
        }
        flameThrowerMeterCoroutine = null;
    }

    void InstantiateInhalerMeter(Slot slotAdd, Item itemToAdd)
    {
        slotAdd.floatMeterFill.enabled = true;
        slotAdd.floatMeterFill.fillAmount = (float)itemToAdd.intLeft / 6;
    }

    void InstantiateFlameThrowerMeter(Slot slotAdd, Item itemToAdd)
    {
        slotAdd.floatMeterFill.enabled = true;
        slotAdd.floatMeterFill.fillAmount = itemToAdd.floatLeftList[0] / 30.0f;
    }

    void AddActiveBomb(float timeLeft, Slot bombSlot)
    {
        if(activeBombCoroutine == null)
        {
            activeBombCoroutine = StartCoroutine(ActiveBombCoroutine(timeLeft, bombSlot));
        }
    }

    IEnumerator ActiveBombCoroutine(float initDuration, Slot bombSlot)
    {
        hasActiveBomb = true;
        float percentComplete;
        activeBombTimePassed = initDuration;

        if(initDuration < TIME_TO_BOMB_BLINK) //If the initial duration is greater than or equal to the flash duration, we skip over the main flash and go straight to the fade because on the old coroutine we were at the fade.
        {
            percentComplete = initDuration / TIME_TO_BOMB_BLINK; // Formula for finding the starting percentcomplete is initDuration (timepassed) / 2.0 (TIME_TO_BOMB_BLINK). Ex: 1.87 / 2.0 = 0.935. Ex: 0 (bomb starts at 0) / 2.0 = 0.

            while(percentComplete < 1.0f)
            {
                percentComplete += Time.deltaTime / TIME_TO_BOMB_BLINK;
                activeBombTimePassed += Time.deltaTime;
                yield return null;
            }
        }

        int startLoopPosInt = 0;
        float initDurationTrunc;
        bool finishedOneLoop = false;

        if(initDuration >= 2.0f)
        {
            initDurationTrunc = Mathf.Floor(initDuration*10)/10;  //Truncate to one decimal
            float startLoopPos = (initDurationTrunc - TIME_TO_BOMB_BLINK) * 10;
            startLoopPosInt  = (int)startLoopPos;
        }

        for(int i = startLoopPosInt; i < 20; i++) // Formula for finding the number of iterations is (timepassed truncated to one decimal) (timepassed - TiME_TO_BOMB_BLINK) * 10. Ex: TIme passed 2.56 = 2.5 - 2.0 = 0.5 * 10 = 5
        {
            activeBombIsBlinking = true;

            if(switchedActiveBombSlot)
            {
                RecalculateBombSlot(ref bombSlot);
                switchedActiveBombSlot = false;
            }

            if(i % 2 == 0) // Make the bomb blink, because it is about to explode.
            {
                if(switchedActiveBombSlot)
                {
                    RecalculateBombSlot(ref bombSlot);
                    switchedActiveBombSlot = false;
                }
                bombSlot.itemImage.enabled = false;
            }
            else
            {
                if(switchedActiveBombSlot)
                {
                    RecalculateBombSlot(ref bombSlot);
                    switchedActiveBombSlot = false;
                }
                bombSlot.itemImage.enabled = true;
            }

            if(initDuration >= 2.0f && !finishedOneLoop) // If the init duration is greater than 2.0 (meaning the bomb was in the blinking loop), we must calculate the percent complete for the first loop (after first loop is complete the percent complete is always 0.0) to get the sync correct.
                percentComplete =  ((activeBombTimePassed - TIME_TO_BOMB_BLINK) - (BOMB_BLINK_DURATION * startLoopPosInt)) / BOMB_BLINK_DURATION; // The forumla for  this is ( (timepassed - TIME_TO_BOMB_BLINK (2.0) ) - (BOMB_BLINK_DURATION * startloopPosInt) ) / BOMB_BLINK_DURATION (2.0)
            else                                                                                            // Ex: Timepassed(init duration) = 2.567. = ((2.567 - 2.0) - (0.1 * 5)) / 0.1 = ((0.567) - (0.5)) / 0.1 = 0.067 / 0.1 = ***0.67***
                percentComplete = 0.0f;

            while(percentComplete < 1.0f)
            {

                percentComplete += Time.deltaTime / BOMB_BLINK_DURATION;
                activeBombTimePassed += Time.deltaTime;
                yield return null;
                finishedOneLoop = true;
            }            
        }
        
        print("debug will reset");
        ResetActiveBombCoroutine(true);
        actor.ExplodeBombInInventoryOnActor(); // Create an explosion on the player, because the bomb blew up in their inventory.
    }

    void RecalculateBombSlot(ref Slot bombslot)
    {
        for(int i = 0; i < items.Length; i++) // Find the slot with the bomb, to decrease the ammo count.
        {
            if(items[i] != null)
            {
                if(items[i].itemType == Item.ItemType.BOMB)
                    bombslot = slots[i].GetComponent<Slot>();
            }
        }
    }
    public void ResetActiveBombCoroutine(bool decrementAmmo)
    {
        int bombSlotNum = -1; // set to -1 since c# logic is unaware that a bomb has to be in the inventory (avoids unassigned error).

        for(int i = 0; i < items.Length; i++) // Find the slot with the bomb, to decrease the ammo count.
        {
            if(items[i] != null)
            {
                if(items[i].itemType == Item.ItemType.BOMB)
                    bombSlotNum = i;
            }
        }

        slots[bombSlotNum].GetComponent<Slot>().itemImage.enabled = true;
        if(decrementAmmo) // When an active bomb is fired, the ammo decrement module is called from the weapon class, as such we dont want to decrement here also becuase it will decrement the ammo count twice instead of once.
            DecrementAmmoCount(bombSlotNum);
        activeBombIsBlinking = false;
        hasActiveBomb = false; //Reset
        activeBombTimePassed = 0;
        activeBombCoroutine = null;
    }

    void CheckAllNonBombSlotImagesAreEnableAfterSwap()
    {
        for(int i=0; i < items.Length; i++)
        {
            if(items[i] != null)
            {
                if(items[i].itemType != Item.ItemType.BOMB)
                {
                    Slot checkedSlot = slots[i].GetComponent<Slot>();
                    if(!checkedSlot.itemImage.enabled)
                        checkedSlot.itemImage.enabled = true;
                }
            }
        }
    }

    void AddTimeLeftToList(bool addToIntList, int slotNum, Item itemToAdd)
    {
        if(addToIntList)
        {
            if(itemToAdd.intLeftList.Count == 0) // Only one item is there because, there is no list yet on the object. (This means it spawned from a box, the list is added when the item is added to the players inventory.)
            {
                items[slotNum].intLeftList.Add(itemToAdd.intLeft);
            }
            else
            {
                foreach(int intLeft in itemToAdd.intLeftList) // Add each int in the items intleft list to the inventories intleft list.
                {
                    items[slotNum].intLeftList.Add(intLeft);
                }
            }
        }
        else
        {
            if(itemToAdd.floatLeftList.Count == 0)
            {
                items[slotNum].floatLeftList.Add(itemToAdd.floatleft);
            }
            else
            {
                foreach(float floatLeft in itemToAdd.floatLeftList)
                {
                    items[slotNum].floatLeftList.Add(floatLeft);
                }
            }
        }
    }

    public void IncrementAmmoCount()
    {
        Slot currentSlot = slots[selectedSlot].GetComponent<Slot>();

        int ammo = Int32.Parse(currentSlot.qtyText.text);
        items[selectedSlot].quantity++;
        ammo++;
        currentSlot.qtyText.text = ammo.ToString();
    }

    public void IncrementAmmoCount(int num)
    {
        Slot currentSlot = slots[num].GetComponent<Slot>();

        int ammo = Int32.Parse(currentSlot.qtyText.text);
        items[num].quantity++;
        ammo++;
        currentSlot.qtyText.text = ammo.ToString();
    }

    public void FreezeInventory(bool freezeInventory) // If the player is in flight with a cape or is floating with it, do not allow them to change the inventory
    {
        if(freezeInventory)
            canChangeInventory = false;
        else
            canChangeInventory = true;
    }
 
 
    public bool CheckInventoryForFlameThrower()
    {
        foreach(Item itemInv in items)
        {
            if(itemInv != null)
            {
                if(itemInv.itemType == Item.ItemType.FLAMETHROWER)
                    return true;
            }
        }
        return false;
    }

    public void TransferInventoryToNewActor(GameObject[] oldSlots, Item[] oldItems, int oldSelectedSlotNum, Coroutine oldFirebirdCoroutine, Coroutine oldQuadDamageCoroutine, int oldQuadDamageTimeLeft, bool oldInventoryHasActiveBomb, float oldActiveBombTimeLeft, int oldNumberOfItemsInventory) // When we 
    {
        transferedInventory = true; // Set to make sure that the inventory does not instantiate new blank slots.

        for(int i = 0; i < oldItems.Length; i++) // Transfer over old items (inventory is shallow copy.)
        {
            if(oldItems[i] != null)
            {
                items[i] = Instantiate(oldItems[i]);
            }
        }

        for(int i = 0; i < oldSlots.Length; i++) // Transfer over old slots (inventory is shallow copy.)
        {
            print("i"+i);
            if(oldSlots[i] == null)
                print("debug slot is null");
            slots[i] = Instantiate(oldSlots[i]);
            slots[i].name = "ItemSlot_" + i;
            slots[i].transform.SetParent(gameObject.transform.GetChild(0).transform);
            slots[i].transform.localScale = new Vector3(0.3125f,0.3125f,0.4166666f); // When we transfer the slots, the size is scaled down, reset to original size.
        }

        if(oldFirebirdCoroutine != null) // There was an old firebirdcoroutine running, set it in the new inventory
        {
            Slot firebirdSlot = slots[selectedSlot].GetComponent<Slot>(); // Set to null, since C# doesn't recognize that the firebird object has to be in the slots array
            int firebirdSlotNum = -1;
            FindFireBirdSlotAndSlotNumber(ref firebirdSlot, ref firebirdSlotNum);

            EnableFirebirdCounter(items[firebirdSlotNum].intLeftList[0]);
        }

        if(oldQuadDamageCoroutine != null) // There was an old quaddamage running, set it in the new inventory
        {
            EnableQuadDamage(oldQuadDamageTimeLeft);
        }

        if(oldInventoryHasActiveBomb) // Transfer over the active bomb.
        {
            Slot activeBombSlot = null;

            for(int i = 0; i < items.Length; i++) // Find the slot with bombs.
            {
                if(items[i] != null)
                {
                    if(items[i].itemType == Item.ItemType.BOMB)
                        activeBombSlot = slots[i].GetComponent<Slot>();
                }
            }

            activeBombCoroutine = StartCoroutine(ActiveBombCoroutine(oldActiveBombTimeLeft,activeBombSlot));
        }

        numItemsInInventory = oldNumberOfItemsInventory;

        Slot selected = slots[selectedSlot].gameObject.GetComponent<Slot>();
        selected.ToggleSlotActive(false);

        selectedSlot = oldSelectedSlotNum;
        selected = slots[selectedSlot].gameObject.GetComponent<Slot>();
        selected.ToggleSlotActive(true);
    }
}

