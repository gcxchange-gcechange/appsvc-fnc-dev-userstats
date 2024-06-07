using System;
using System.Collections.Generic;

namespace appsvc_fnc_dev_userstats
{
    public class usersData
    {
        public string Id { get; set; }
        public DateTimeOffset? creationDate { get; set; }
        public string mail { get; set; }
    }

    public class groupsData
    {
        public string displayName;
        public int countMember;
        public string groupId;
        public string creationDate;
        public string description;
        public IEnumerable<string> groupType;
        public List<string> userlist;
    }

    public class activeuserData
    {
        public string userid;
        public string UserDisplayName;
    }

    public class countactiveuserData
    {
        public string name;
        public int countActiveusers;
        public List<ActiveUserCountByDomain> countByDomain;
    }

    public class ActiveUserCountByDomain
    {
        public string domain;
        public int count;

        public ActiveUserCountByDomain(string domainName, int userCount) {
            domain = domainName;
            count = userCount;
        }
    }

    public class SingleGroup
    {
        public string displayName;
        public int countMember;
        public string groupId;
        public string creationDate;
        public string description;
        public IEnumerable<string> groupType;
        public List<String> userlist;

        public SingleGroup(string displayName, int countMember, string groupId, string creationDate, string description, IEnumerable<string> groupType, List<String> userlist)
        {
            this.displayName = displayName;
            this.countMember = countMember;
            this.groupId = groupId;
            this.creationDate = creationDate;
            this.description = description;
            this.groupType = groupType;
            this.userlist = userlist;
        }
    }

    public class Group
    {
        public string groupId;
        public string displayName;
        public string remainingStorage;
        public string usedStorage;
        public string totalStorage;

        public List<Drives> drivesList;


        public Group(string groupId, string displayName, string remainingStorage, string usedStorage, string totalStorage, List<Drives> drivesList)
        {
            this.groupId = groupId;
            this.displayName = displayName;
            this.remainingStorage = remainingStorage;
            this.usedStorage = usedStorage;
            this.totalStorage = totalStorage;
            this.drivesList = drivesList ?? new List<Drives>();

        }
    }

    public class Folders
    {
        public string fileId;
        public string fileName;
        public string createdDate { get; set; }
        public string lastModifiedDate { get; set; }

        public Folders(string fileId, string fileName, string createdDate, string lastModifiedDate)
        {
            this.fileId = fileId;
            this.fileName = fileName;
            this.createdDate = createdDate;
            this.lastModifiedDate = lastModifiedDate;
        }
    }

    public class Drives
    {
        public string driveId;
        public string driveName;
        public string driveType;
        public List<Folders> folderListItems;

        public Drives(string driveId, string driveName, string driveType, List<Folders> folderListItems)
        {
            this.driveId = driveId;
            this.driveName = driveName;
            this.driveType = driveType;
            this.folderListItems = folderListItems;
        }
    }
}