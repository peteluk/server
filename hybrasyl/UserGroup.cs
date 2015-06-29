/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2013 Justin Baugh (baughj@hybrasyl.com)
 * (C) 2015 Project Hybrasyl (info@hybrasyl.com)
 *
 * Authors:   Luke Segars   <luke@lukesegars.com>
 */

using Hybrasyl.Objects;
using Hybrasyl.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;


// TODO (testing): Join a group when starting from no group.
// TODO (testing): Invite someone to a group when you're already in one.
// TODO (testing): Leave a group.
// TODO (testing): Ensure group is deleted when all users leave.
// TODO (testing): Experience is being distributed.
// TODO (testing): Group whispers work (both as sender and receiver).
// TODO (testing): Players highlighted when they press "g".
// TODO (testing): User leaves group when they disconnect.
// TODO (testing): appropriate messaging comes through when you join / leave a group.
// TODO (testing): Appropriate messaging comes through when someone else joins / leaves a group.
// TODO (testing): Make sure a user can't be invited into a group if they're already in one.
// TODO (testing): Group information appears in Group window.
namespace Hybrasyl
{
    /**
     * This class defines a group of users.
     */
    public class UserGroup
    {
        public static readonly ILog Logger =
            LogManager.GetLogger(
            System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        // Group-related info
        public List<User> Members { get; private set; }
        public DateTime CreatedOn { get; private set; }
        public Dictionary<Class, uint> ClassCount { get; private set; }
        public uint MaxMembers = 0;

        private delegate Dictionary<uint, int> DistributionFunc(User source, int full);
        private DistributionFunc ExperienceDistributionFunc;

        public UserGroup(User founder)
        {
            Members = new List<User>();
            ClassCount = new Dictionary<Class, uint>();
            foreach (var cl in Enum.GetValues(typeof(Class)).Cast<Class>())
            {
                ClassCount[cl] = 0;
            }

            Logger.InfoFormat("Creating new group with {0} as founder.", founder.Name);
            // Distribute full experience to everyone with a bonus if a member of each
            // class is present.
            ExperienceDistributionFunc = Distribution_AllClassBonus;

            Add(founder);
            CreatedOn = DateTime.Now;
        }

        // TODO: remove this when i've confirmed that groups are actually being deleted.
        ~UserGroup()
        {
            Logger.Debug("Cleaning up group.");
        }

        // Group-related functions and accessors
        public bool Add(User user)
        {
            // You can only join a group if you're not already a member in another group.
            // Check this condition and notify the invitee and existing group members if
            // there's an issue.
            if (user.Grouped)
            {
                user.SendMessage("You're already in a group.", MessageTypes.SYSTEM);

                foreach (var member in Members)
                {
                    member.SendMessage(String.Format("{0} is in another group.", user.Name), MessageTypes.SYSTEM);
                }

                // If this fails when the group only contains one other person, the group should be abandoned.
                if (Count == 1)
                {
                    Remove(Members[0]);
                }

                return false;
            }

            // Send a message to notify everyone else that someone's joined.
            foreach (var member in Members)
            {
                member.SendMessage(user.Name + " has joined your group.", MessageTypes.SYSTEM);
            }

            Members.Add(user);
            user.Group = this;
            ClassCount[user.Class]++;
            MaxMembers = (uint)Math.Max(MaxMembers, Members.Count);

            // Send a special message to the new user. This is different than how USDA does it but
            // provides more flexibility.
            user.SendMessage("You've joined a group.", MessageTypes.SYSTEM);
            return true;
        }

        public void Remove(User user)
        {
            Members.Remove(user);
            user.Group = null;
            ClassCount[user.Class]--;

            // If this has ever been a true group from a user's perspective, talk about it. Otherwise
            // don't send user-facing messages.
            if (MaxMembers > 1)
            {
                foreach (var member in Members)
                {
                    member.SendMessage(user.Name + " has left your group.", MessageTypes.SYSTEM);
                }
                user.SendMessage("You've left a group.", MessageTypes.SYSTEM);
            }

            // If there's only one person left, disband the grounp.
            if (Count == 1)
            {
                Remove(Members[0]);
            }
        }

        public int Count
        {
            get { return Members.Count; }
        }

        public bool ContainsAllClasses()
        {
            return (ClassCount[Enums.Class.Monk] > 0 &&
                ClassCount[Enums.Class.Priest] > 0 &&
                ClassCount[Enums.Class.Rogue] > 0 &&
                ClassCount[Enums.Class.Warrior] > 0 &&
                ClassCount[Enums.Class.Wizard] > 0);
        }

        /**
         * Distribute a pool of experience across members of the group.
         */
        public void ShareExperience(User source, int experience)
        {
            Dictionary<uint, int> share = ExperienceDistributionFunc(source, experience);

            for (int i = 0; i < Members.Count; i++)
            {
                // Note: this will only work for positive numbers at this point.
                Members[i].GiveExperience((uint)share[Members[i].Id]);
            }
        }

        /**
         * Distribution functions. Should be assigned to ExperienceDistributionFunc or a similar
         * function to be put to use. See the constructor of UserGroup for an example.
         */
        private Dictionary<uint, int> Distribution_Full(User source, int full)
        {
            Dictionary<uint, int> share = new Dictionary<uint, int>();

            foreach (var member in Members)
            {
                share[member.Id] = full;
            }

            return share;
        }

        private Dictionary<uint, int> Distribution_AllClassBonus(User source, int full)
        {
            // Check to see if at least one representative from each class is in the group.
            if (ContainsAllClasses())
            {
                full = (int)(full * 1.10);
            }

            return Distribution_Full(source, full);
        }
    }
}
