using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//The Medic will remain within a single train car, attacking the nearest werewolf and healing friendlies over time
//If there are no werewolves, the medic will randomly wander within the car
public class Medic_Behavior : MonoBehaviour
{
	//Contains the medics's stats
	public Medic medic;

	//Sprite Animator
	public Animator myAnimator;

	//The medic's collider
	private Collider2D myCollider;

	//Werewolf being targeted by the medic
	public GameObject target;

	//Train car the medic is in
	public GameObject currentCar;

	//Time variable, used to determine when to attack
	private float startTime;

	//Time variable, used to determine when to heal
	private float startHealTime;

	//Random point in the car the medic will walk to when not in combat
	private Vector2 wanderPoint;

	//Constant speed multiplier for healing
	private float healingSpeedMult = 0.2f;

	//Medic's maximum HP
	private int maxHP;

	//Whether the medic is in combat
	private bool inCombat = false;

	//Whether the medic is in a panicked state
	private bool panicked = false;

	//Whether the medic is randomly walking around the car
	private bool isWandering = false;

	//Attack and Movement Speed Multipliers
	private float attackMult = 0.5f;
	private float speedMult = 0.75f;

	public SpriteRenderer myRenderer;

	// Start is called before the first frame update
	void Start()
	{
		medic = new Medic();

		maxHP = medic.hp;

		startTime = Time.time;

		startHealTime = Time.time;

		myCollider = GetComponent<Collider2D>();

		wanderPoint = WanderAround(currentCar.GetComponent<Collider2D>().bounds);
	}

	// Update is called once per frame
	void Update()
	{
		//Kill medic if at 0 health
		if (medic.hp <= 0)
		{
			//Character death has a chance to reduce morale of characters within the car
			ReduceFriendlyMorale();

			//Reduce medic count for train car
			currentCar.GetComponent<Train_Car_Info>().trainCar.numMedics--;

			Destroy(gameObject);
		}

		//Passive Healing
		float deltaT = Time.time - startHealTime;
		if (deltaT >= medic.speed * healingSpeedMult){
			HealFriendlies();
			startHealTime = Time.time;
		}

		//Check if in combat
		if (inCombat)
		{
			//Make sure target is still in attack range
			if (target != null && myCollider.bounds.Intersects(target.GetComponent<Collider2D>().bounds))
			{
				//Attack

				deltaT = Time.time - startTime;

				//Check if enough time has passed since last attack
				if (deltaT >= medic.speed * attackMult)
				{
					//Attack Target
					int attackChance = Random.Range(1, 11);

					//Check if attack hits
					if (attackChance <= medic.accuracy)
					{
						//Make sure target has not been killed
						if (target != null)
						{
							//Reduce target's HP
							target.BroadcastMessage("ChangeHP", -medic.melee);
						}
					}

					startTime = Time.time;
				}

				//If target is dead, combat ends
				if (target == null)
				{
					inCombat = false;
					startTime = Time.time;

					SetAnimation(gameObject.transform.position, gameObject.transform.position, false);
				}
				else
				{
					SetAnimation(gameObject.transform.position, target.transform.position, true);
				}
			}
			else
			{
				//Target is no longer in range
				target = null;
				inCombat = false;
				startTime = Time.time;

				SetAnimation(gameObject.transform.position, gameObject.transform.position, false);
			}
		}
		else
		{
			//Move

			//Select a target
			target = FindNearestTarget();

			if (target != null && isWandering)
			{
				isWandering = false;
				startTime = Time.time;
			}

			if (target != null)
			{
				//If a target is found, move to them
				deltaT = Time.time - startTime;

				Vector2 newPos = Vector2.MoveTowards(gameObject.transform.position, target.transform.position, medic.speed * deltaT * speedMult);
				newPos = PosInTrainCar(newPos, currentCar.GetComponent<Collider2D>().bounds);

				SetAnimation(gameObject.transform.position, newPos, false);

				gameObject.transform.position = newPos;

				//Upon collision with target, begin combat
				if (myCollider.bounds.Intersects(target.GetComponent<Collider2D>().bounds))
				{
					inCombat = true;
				}

				startTime = Time.time;
			}
			else
			{
				//If no target is found, move to a random point in the car
				isWandering = true;

				//Check if the current destination exists and has been reached
				if (wanderPoint == null || Vector2.Distance(gameObject.transform.position, wanderPoint) < .001f)
				{
					//If there is not a destination selected or it has been reached, select a new destination

					float currentTime = Time.time;
					deltaT = currentTime - startTime;

					//After pausing at the destination for a short time, choose a new destination
					if (deltaT >= 3.0f)
					{
						wanderPoint = WanderAround(currentCar.GetComponent<Collider2D>().bounds);
						startTime = Time.time;
					}

					SetAnimation(gameObject.transform.position, gameObject.transform.position, false);
				}
				else
				{
					//Move towards destination
					deltaT = Time.time - startTime;

					Vector2 newPos = Vector2.MoveTowards(gameObject.transform.position, wanderPoint, medic.speed * deltaT * speedMult);
					newPos = PosInTrainCar(newPos, currentCar.GetComponent<Collider2D>().bounds);

					SetAnimation(gameObject.transform.position, newPos, false);

					gameObject.transform.position = newPos;
					
					startTime = Time.time;
				}
			}
		}
	}

	//Change the medic's HP
	public void ChangeHP(int delta)
	{
		//Check whether the medic is being attacked or healed
		if (delta < 0)
		{
			//Incoming Attack

			//Decrease attack by the medic's armor
			delta += medic.armor;

			//Make sure an intended attack doesn't heal
			if (delta > 0)
			{
				delta = 0;
			}

			medic.hp += delta;
		}
		else
		{
			//Incoming Healing
			if (medic.hp == maxHP){
				return;
			}
			
			//Make sure the medic isn't healed over full HP
			if (medic.hp + delta >= maxHP)
			{
				medic.hp = maxHP;
			}
			else
			{
				medic.hp += delta;
			}
		}

		if (delta > 0)
		{
			StartCoroutine("IndicateHeal");
		}
		else
		{
			StartCoroutine("IndicateDamage");
		}
	}

	//Change medic's morale by a set amount
	public void ChangeMorale(int amount)
	{

		medic.morale += amount;

		//Check whether the change in morale causes them to become panicked or stop being panicked
		if (panicked && medic.morale > 0)
		{
			//Medic is no longer panicked
			panicked = false;

			medic.shoot += 1;
			medic.melee += 1;
			medic.speed -= 1;
		}
		else if (!panicked && medic.morale < 0)
		{
			//Medic becomes panicked
			panicked = true;

			medic.shoot -= 1;
			medic.melee -= 1;
			medic.speed += 1;
		}
	}

	//A friendly death may cause a reduction in morale
	public void ChangeMoraleDeath()
	{
		//50% chance the medic's morale will be reduced
		int chance = Random.Range(0, 2);

		if (chance == 0)
		{
			medic.morale -= 1;
		}

		//If morale reaches zero, the medic becomes panicked
		if (medic.morale <= 0 && !panicked)
		{
			panicked = true;

			medic.shoot -= 1;
			medic.melee -= 1;
			medic.speed += 1;
		}
	}

	//Find all friendlies in the same train car and check if their morale is reduced
	public void ReduceFriendlyMorale()
	{
		//An array of all friendlies on the train
		GameObject[] friendlies;
		friendlies = GameObject.FindGameObjectsWithTag("Friendly");

		foreach (GameObject friendly in friendlies)
		{
			//Make sure friendly is in the same car
			if (currentCar.GetComponent<Collider2D>().bounds.Intersects(friendly.GetComponent<Collider2D>().bounds))
			{
				//If friendly is alive
				if (friendly != null)
				{
					//Check if morale should be changed
					friendly.BroadcastMessage("ChangeMoraleDeath");
				}
			}
		}
	}

	//Select an enemy to target
	public GameObject FindNearestTarget()
	{
		//An array of all enemies on train
		GameObject[] enemies;
		enemies = GameObject.FindGameObjectsWithTag("Enemy");

		GameObject closest = null;
		float distance = Mathf.Infinity;
		Vector3 position = transform.position;

		foreach (GameObject enemy in enemies)
		{
			//Make sure enemy is in the same car
			if (currentCar.GetComponent<Collider2D>().bounds.Intersects(enemy.GetComponent<Collider2D>().bounds))
			{
				//Check if the enemy is closer than the currently selected enemy
				Vector3 diff = enemy.transform.position - position;
				float curDistance = diff.sqrMagnitude;

				if (curDistance < distance)
				{
					closest = enemy;
					distance = curDistance;
				}
			}
		}

		return closest;
	}

	//Heal all friendlies in the same train car
	public void HealFriendlies()
	{
		//An array of all friendlies on train
		GameObject[] friendlies;
		friendlies = GameObject.FindGameObjectsWithTag("Friendly");

		foreach (GameObject friendly in friendlies)
		{
			//Make sure friendly is in the same car
			if (currentCar.GetComponent<Collider2D>().bounds.Intersects(friendly.GetComponent<Collider2D>().bounds))
			{
				//Heal friendly
				if (friendly != null)
				{
					friendly.BroadcastMessage("ChangeHP", medic.heal);
				}
			}
		}
	}

	//Randomly select a point in the train car to wander to
	public Vector2 WanderAround(Bounds bounds)
	{
		return new Vector2(Random.Range(bounds.min.x + 0.37f, bounds.max.x - 0.37f), Random.Range(bounds.min.y + 0.75f, bounds.max.y - 0.75f));
	}

	//Make sure the medic's new position isn't outside the train car's bounds
	public Vector2 PosInTrainCar(Vector2 pos, Bounds carBounds)
	{
		if (pos.x < carBounds.min.x + 0.37f)
		{
			pos.x = carBounds.min.x + 0.37f;
		}
		else if (pos.x > carBounds.max.x - 0.37f)
		{
			pos.x = carBounds.max.x - 0.37f;
		}

		if (pos.y < carBounds.min.y + 0.75f)
		{
			pos.y = carBounds.min.y + 0.75f;
		}
		else if (pos.y > carBounds.max.y - 0.75f)
		{
			pos.y = carBounds.max.y - 0.75f;
		}

		return pos;
	}

	//Set animation according to sprite movement
	private void SetAnimation(Vector2 startPos, Vector2 newPos, bool hasTarget)
	{
		//Change in X and Y Positions
		float deltaX = newPos.x - startPos.x;
		float deltaY = newPos.y - startPos.y;

		//Check if medic is moving
		if (!hasTarget && (Mathf.Abs(deltaX) > .0001f || Mathf.Abs(deltaY) > .0001f))
		{
			myAnimator.SetBool("moving", true);

			if (Mathf.Abs(deltaX) > Mathf.Abs(deltaY))
			{
				myAnimator.SetBool("forward", false);
				myAnimator.SetBool("backward", false);

				if (deltaX > 0)
				{
					myAnimator.SetBool("right", true);
					myAnimator.SetBool("left", false);
				}
				else
				{
					myAnimator.SetBool("left", true);
					myAnimator.SetBool("right", false);
				}
			}
			else
			{
				myAnimator.SetBool("left", false);
				myAnimator.SetBool("right", false);

				if (deltaY > 0)
				{
					myAnimator.SetBool("backward", true);
					myAnimator.SetBool("forward", false);
				}
				else
				{
					myAnimator.SetBool("forward", true);
					myAnimator.SetBool("backward", false);
				}
			}
		}
		else if (!hasTarget)
		{
			myAnimator.SetBool("moving", false);

			myAnimator.SetBool("forward", false);
			myAnimator.SetBool("backward", false);
			myAnimator.SetBool("left", false);
			myAnimator.SetBool("right", false);
		}
		else
		{
			myAnimator.SetBool("moving", false);

			if (Mathf.Abs(deltaX) > Mathf.Abs(deltaY))
			{
				myAnimator.SetBool("forward", false);
				myAnimator.SetBool("backward", false);

				if (deltaX > 0)
				{
					myAnimator.SetBool("right", true);
					myAnimator.SetBool("left", false);
				}
				else
				{
					myAnimator.SetBool("left", true);
					myAnimator.SetBool("right", false);
				}
			}
			else
			{
				myAnimator.SetBool("left", false);
				myAnimator.SetBool("right", false);

				if (deltaY > 0)
				{
					myAnimator.SetBool("backward", true);
					myAnimator.SetBool("forward", false);
				}
				else
				{
					myAnimator.SetBool("forward", true);
					myAnimator.SetBool("backward", false);
				}
			}
		}
	}

	//Flash red when damaged
	IEnumerator IndicateDamage()
	{
		myRenderer.color = new Color(1.0f, 0.0f, 0.0f, 1.0f);
		yield return new WaitForSeconds(.1f);
		myRenderer.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
	}


	//Flash green when healed
	IEnumerator IndicateHeal()
	{
		myRenderer.color = new Color(0.0f, 1.0f, 0.0f, 1.0f);
		yield return new WaitForSeconds(.1f);
		myRenderer.color = new Color(1.0f, 1.0f, 1.0f, 1.0f);
	}
}
