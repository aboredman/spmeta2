﻿using System;
using System.Collections.Generic;
using Microsoft.SharePoint.Client;
using SPMeta2.Common;
using SPMeta2.CSOM.Common;
using SPMeta2.CSOM.Extensions;
using SPMeta2.CSOM.ModelHosts;
using SPMeta2.Definitions;
using SPMeta2.Definitions.Base;
using SPMeta2.ModelHandlers;
using SPMeta2.Services;
using SPMeta2.Utils;

namespace SPMeta2.CSOM.ModelHandlers
{
    public class SecurityGroupModelHandler : CSOMModelHandlerBase
    {
        #region properties

        #endregion

        #region methods

        public override void WithResolvingModelHost(object modelHost, DefinitionBase model, Type childModelType, Action<object> action)
        {
            var webModelHost = modelHost.WithAssertAndCast<SiteModelHost>("modelHost", value => value.RequireNotNull());

            var web = webModelHost.HostSite.RootWeb;
            var securityGroupModel = model as SecurityGroupDefinition;

            if (web != null && securityGroupModel != null)
            {
                var context = web.Context;

                context.Load(web, tmpWeb => tmpWeb.SiteGroups);
                context.ExecuteQueryWithTrace();

                var currentGroup = FindSecurityGroupByTitle(web.SiteGroups, securityGroupModel.Name);

                //action(new ModelHostContext
                action(new SecurityGroupModelHost
                {
                    SecurableObject = web,
                    SecurityGroup = currentGroup
                });

                currentGroup.Update();
                context.ExecuteQueryWithTrace();
            }
            else
            {
                action(modelHost);
            }
        }

        public override void DeployModel(object modelHost, DefinitionBase model)
        {
            var webModelHost = modelHost.WithAssertAndCast<SiteModelHost>("modelHost", value => value.RequireNotNull());

            var web = webModelHost.HostSite.RootWeb;
            var securityGroupModel = model.WithAssertAndCast<SecurityGroupDefinition>("model", value => value.RequireNotNull());

            var context = web.Context;

            // well, this should be pulled up to the site handler and init Load/Exec query
            context.Load(web, tmpWeb => tmpWeb.SiteGroups);
            context.ExecuteQueryWithTrace();

            var currentGroup = FindSecurityGroupByTitle(web.SiteGroups, securityGroupModel.Name);

            InvokeOnModelEvent(this, new ModelEventArgs
            {
                CurrentModelNode = null,
                Model = null,
                EventType = ModelEventType.OnProvisioning,
                Object = currentGroup,
                ObjectType = typeof(Group),
                ObjectDefinition = model,
                ModelHost = modelHost
            });

            if (currentGroup == null)
            {
                TraceService.Information((int)LogEventId.ModelProvisionProcessingNewObject, "Processing new security group");

                currentGroup = web.SiteGroups.Add(new GroupCreationInformation
                {
                    Title = securityGroupModel.Name,
                    Description = securityGroupModel.Description ?? string.Empty,
                });
            }
            else
            {
                TraceService.Information((int)LogEventId.ModelProvisionProcessingExistingObject, "Processing existing security group");
            }

            currentGroup.Title = securityGroupModel.Name;
            currentGroup.Description = securityGroupModel.Description ?? string.Empty;
            currentGroup.OnlyAllowMembersViewMembership = securityGroupModel.OnlyAllowMembersViewMembership;

            InvokeOnModelEvent(this, new ModelEventArgs
            {
                CurrentModelNode = null,
                Model = null,
                EventType = ModelEventType.OnProvisioned,
                Object = currentGroup,
                ObjectType = typeof(Group),
                ObjectDefinition = model,
                ModelHost = modelHost
            });

            currentGroup.Update();
            context.ExecuteQueryWithTrace();
        }

        protected Group FindSecurityGroupByTitle(IEnumerable<Group> siteGroups, string securityGroupTitle)
        {
            // gosh, who cares ab GetById() methods?! Where GetByName()?!

            TraceService.VerboseFormat((int)LogEventId.ModelProvisionCoreCall, "Resolving security group by title: [{0}]", securityGroupTitle);

            foreach (var securityGroup in siteGroups)
            {
                if (System.String.Compare(securityGroup.Title, securityGroupTitle, System.StringComparison.OrdinalIgnoreCase) == 0)
                    return securityGroup;
            }

            return null;
        }

        #endregion

        public override Type TargetType
        {
            get { return typeof(SecurityGroupDefinition); }
        }
    }
}
