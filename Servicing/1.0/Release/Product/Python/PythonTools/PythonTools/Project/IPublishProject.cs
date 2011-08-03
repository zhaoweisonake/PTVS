﻿/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the Apache License, Version 2.0, please send an email to 
 * vspython@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Apache License, Version 2.0.
 *
 * You must not remove this notice, or any other, from this software.
 *
 * ***************************************************************************/

using System.Collections.Generic;

namespace Microsoft.PythonTools.Project {
    public interface IPublishProject {
        /// <summary>
        /// Gets the list of files which need to be published.
        /// </summary>
        IList<IPublishFile> Files {
            get;
        }

        /// <summary>
        /// Gets the root directory of the project.
        /// </summary>
        string ProjectDir {
            get;
        }

        /// <summary>
        /// Gets or sets the progress of the publishing.
        /// </summary>
        int Progress {
            get;
            set;
        }
    }
}