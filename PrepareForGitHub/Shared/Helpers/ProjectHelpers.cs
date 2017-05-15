﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace PrepareForGitHub.Shared.Helpers
{
    static class ProjectHelpers
    {
        const string _extensibilityProjectGuid = "{82b43b9b-a64c-4715-b499-d71e9ca2bd60}";
        public static DTE2 DTE;

        static IServiceProvider _serviceProvider;

        public static void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            DTE = (DTE2)serviceProvider.GetService(typeof(DTE));
        }

        public static bool IsExtensibilityProject(this Project project)
        {
            if (project == null)
                return false;

            var solution = (IVsSolution)_serviceProvider.GetService(typeof(IVsSolution));

            if (solution == null)
                return false;

            IVsHierarchy hierarchy;
            ErrorHandler.ThrowOnFailure(solution.GetProjectOfUniqueName(project.UniqueName, out hierarchy));

            var aggregatableProject = hierarchy as IVsAggregatableProject;

            if (aggregatableProject == null)
                return false;

            string projectTypeGuids;
            ErrorHandler.ThrowOnFailure(aggregatableProject.GetAggregateProjectTypeGuids(out projectTypeGuids));

            return projectTypeGuids != null && projectTypeGuids.IndexOf(_extensibilityProjectGuid, StringComparison.OrdinalIgnoreCase) > -1;
        }

        /// <summary>
        /// Returns either a Project or ProjectItem. Returns null if Solution is Selected
        /// </summary>
        /// <returns></returns>
        public static object GetSelectedItem()
        {
            IntPtr hierarchyPointer, selectionContainerPointer;
            object selectedObject = null;
            IVsMultiItemSelect multiItemSelect;
            uint itemId;

            var monitorSelection = (IVsMonitorSelection)Package.GetGlobalService(typeof(SVsShellMonitorSelection));

            try
            {
                monitorSelection.GetCurrentSelection(out hierarchyPointer,
                    out itemId,
                    out multiItemSelect,
                    out selectionContainerPointer);

                IVsHierarchy selectedHierarchy = Marshal.GetTypedObjectForIUnknown(
                    hierarchyPointer,
                    typeof(IVsHierarchy)) as IVsHierarchy;

                if (selectedHierarchy != null)
                {
                    ErrorHandler.ThrowOnFailure(selectedHierarchy.GetProperty(itemId, (int)__VSHPROPID.VSHPROPID_ExtObject, out selectedObject));
                }

                Marshal.Release(hierarchyPointer);
                Marshal.Release(selectionContainerPointer);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex);
            }

            return selectedObject;
        }

        public static IEnumerable<Project> GetAllProjectsInSolution()
        {
            var solution = (IVsSolution)_serviceProvider.GetService(typeof(IVsSolution));

            foreach (IVsHierarchy hier in GetProjectsInSolution(solution))
            {
                Project project = GetDTEProject(hier);
                if (project != null)
                    yield return project;
            }
        }

        public static IEnumerable<IVsHierarchy> GetProjectsInSolution(IVsSolution solution)
        {
            return GetProjectsInSolution(solution, __VSENUMPROJFLAGS.EPF_LOADEDINSOLUTION);
        }

        public static IEnumerable<IVsHierarchy> GetProjectsInSolution(IVsSolution solution, __VSENUMPROJFLAGS flags)
        {
            if (solution == null)
                yield break;

            IEnumHierarchies enumHierarchies;
            Guid guid = Guid.Empty;
            solution.GetProjectEnum((uint)flags, ref guid, out enumHierarchies);
            if (enumHierarchies == null)
                yield break;

            IVsHierarchy[] hierarchy = new IVsHierarchy[1];
            uint fetched;
            while (enumHierarchies.Next(1, hierarchy, out fetched) == VSConstants.S_OK && fetched == 1)
            {
                if (hierarchy.Length > 0 && hierarchy[0] != null)
                    yield return hierarchy[0];
            }
        }

        public static Project GetDTEProject(IVsHierarchy hierarchy)
        {
            if (hierarchy == null)
                throw new ArgumentNullException(nameof(hierarchy));

            object obj;
            hierarchy.GetProperty(VSConstants.VSITEMID_ROOT, (int)__VSHPROPID.VSHPROPID_ExtObject, out obj);
            return obj as Project;
        }

        public static void CheckFileOutOfSourceControl(string file)
        {
            if (!File.Exists(file) || DTE.Solution.FindProjectItem(file) == null)
                return;

            if (DTE.SourceControl.IsItemUnderSCC(file) && !DTE.SourceControl.IsItemCheckedOut(file))
                DTE.SourceControl.CheckOutItem(file);

            FileInfo info = new FileInfo(file);
            info.IsReadOnly = false;
        }

        public static ProjectItem AddFileToProject(this Project project, string file, string itemType = null)
        {
            if (!File.Exists(file))
                return null;

            var item = DTE.Solution.FindProjectItem(file);

            if (item == null)
            {
                item = project.ProjectItems.AddFromFile(file);
                item.SetItemType(itemType);
            }

            return item;
        }

        public static void SetItemType(this ProjectItem item, string itemType)
        {
            try
            {
                if (item == null || item.ContainingProject == null)
                    return;

                item.Properties.Item("ItemType").Value = itemType;
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public static string GetRootFolder(this Project project)
        {
            if (project == null || string.IsNullOrEmpty(project.FullName))
                return null;

            string fullPath;

            try
            {
                fullPath = project.Properties.Item("FullPath").Value as string;
            }
            catch (ArgumentException)
            {
                try
                {
                    // MFC projects don't have FullPath, and there seems to be no way to query existence
                    fullPath = project.Properties.Item("ProjectDirectory").Value as string;
                }
                catch (ArgumentException)
                {
                    // Installer projects have a ProjectPath.
                    fullPath = project.Properties.Item("ProjectPath").Value as string;
                }
            }

            if (string.IsNullOrEmpty(fullPath))
                return File.Exists(project.FullName) ? Path.GetDirectoryName(project.FullName) : null;

            if (Directory.Exists(fullPath))
                return fullPath;

            if (File.Exists(fullPath))
                return Path.GetDirectoryName(fullPath);

            return null;
        }

        ///<summary>Gets the paths to all files included in the selection, including files within selected folders.</summary>
        public static IEnumerable<string> GetSelectedFilePaths()
        {
            return GetSelectedItemPaths()
                .SelectMany(p => Directory.Exists(p)
                    ? Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories)
                    : new[] { p }
                );
        }


        ///<summary>Gets the full paths to the currently selected item(s) in the Solution Explorer.</summary>
        public static IEnumerable<string> GetSelectedItemPaths()
        {
            var items = (Array)DTE.ToolWindows.SolutionExplorer.SelectedItems;
            foreach (UIHierarchyItem selItem in items)
            {
                var item = selItem.Object as ProjectItem;

                if (item != null && item.Properties != null)
                    yield return item.Properties.Item("FullPath").Value.ToString();
            }
        }
    }
}
