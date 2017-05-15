﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace PrepareForGitHub.Shared.Commands
{
    abstract class BaseCommand
    {
        protected BaseCommand(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            ServiceProvider = serviceProvider;
            DTE = serviceProvider.GetService(typeof(SDTE)) as DTE2;

            SetupCommands();
        }

        /// <summary>
        /// Gets the IDE object wrapper.
        /// </summary>
        protected DTE2 DTE { get; }

        protected IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Setups new menu command with handlers.
        /// </summary>
        protected OleMenuCommand AddCommand(Guid menuGroup, int commandID, EventHandler invokeHandler, EventHandler beforeQueryHandler)
        {
            if (invokeHandler == null)
                throw new ArgumentNullException("invokeHandler", "Missing action to perform");

            OleMenuCommandService commandService = GetService<OleMenuCommandService, IMenuCommandService>();
            if (commandService != null)
            {
                OleMenuCommand addCustomToolItem = new OleMenuCommand(invokeHandler, new CommandID(menuGroup, commandID));

                if (beforeQueryHandler != null)
                {
                    addCustomToolItem.BeforeQueryStatus += beforeQueryHandler;
                }

                commandService.AddCommand(addCustomToolItem);
            }

            return null;
        }

        /// <summary>
        /// Gets the specific service.
        /// </summary>
        protected T GetService<T, S>() where T : class
        {
            return ServiceProvider.GetService(typeof(S)) as T;
        }

        /// <summary>
        /// Gets the specific service.
        /// </summary>
        protected T GetService<T>() where T : class
        {
            return ServiceProvider.GetService(typeof(T)) as T;
        }

        /// <summary>
        /// Overriden by child class to setup own menu commands and bind with invocation handlers.
        /// </summary>
        protected abstract void SetupCommands();
    }
}
