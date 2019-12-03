﻿//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Globalization;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlTools.Extensibility;
using Microsoft.Kusto.ServiceLayer.ObjectExplorer.Nodes;
using Microsoft.Kusto.ServiceLayer.DataSource;

namespace Microsoft.Kusto.ServiceLayer.ObjectExplorer.DataSourceModel
{
    /// <summary>
    /// Context object containing key properties needed to query for SMO objects
    /// </summary>
    public class QueryContext
    {
        private Server server;
        private Database database;
        private DataSourceObjectMetadata parent;
        private SmoWrapper smoWrapper;
        private ValidForFlag validFor = 0;

        public IDataSource DataSource { get; private set; }

        /// <summary>
        /// Creates a context object with a server to use as the basis for any queries
        /// </summary>
        /// <param name="server"></param>
        public QueryContext(Server server, IDataSource dataSource, IMultiServiceProvider serviceProvider)
            : this(server, dataSource, serviceProvider, null)
        {
        }

        internal QueryContext(Server server, IDataSource dataSource, IMultiServiceProvider serviceProvider, SmoWrapper serverManager)
        {
            this.server = server;
            this.DataSource = dataSource;
            ServiceProvider = serviceProvider;
            this.smoWrapper = serverManager ?? new SmoWrapper();
        }

        /// <summary>
        /// The server type 
        /// </summary>
        public SqlServerType SqlServerType { get; set; }

        /// <summary>
        /// The server SMO will query against
        /// </summary>
        public Server Server { 
            get
            {
                return GetObjectWithOpenedConnection(server);
            } 
        }

        /// <summary>
        /// Optional Database context object to query against
        /// </summary>
        public Database Database { 
            get
            {
                return GetObjectWithOpenedConnection(database);
            }
            set
            {
                database = value;
            }
        }

        /// <summary>
        /// Parent of a give node to use for queries
        /// </summary>
        public DataSourceObjectMetadata ParentObjectMetadata { 
            get
            {
                return GetObjectWithOpenedConnection(parent);
            }
            set
            {
                parent = value;
            }
        }

        /// <summary>
        /// A query loader that can be used to find <see cref="DataSourceQuerier"/> objects
        /// for specific SMO types
        /// </summary>
        public IMultiServiceProvider ServiceProvider { get; private set; }
        
        /// <summary>
        /// Helper method to cast a parent to a specific type
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T ParentAs<T>()
            where T : TreeNode
        {
            return ParentObjectMetadata as T;
        }

        /// <summary>
        /// Gets the <see cref="ObjectExplorerService"/> if available, by looking it up
        /// from the <see cref="ServiceProvider"/>
        /// </summary>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the <see cref="ServiceProvider"/> is not set or the <see cref="ObjectExplorerService"/>
        /// isn't available from that provider
        /// </exception>
        public ObjectExplorerService GetObjectExplorerService()
        {
            if (ServiceProvider == null)
            {
                throw new InvalidOperationException(SqlTools.Hosting.SR.ServiceProviderNotSet);
            }
            ObjectExplorerService service = ServiceProvider.GetService<ObjectExplorerService>();
            if (service == null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.CurrentCulture, 
                    SqlTools.Hosting.SR.ServiceNotFound, nameof(ObjectExplorerService)));
            }

            return service;
        }

        /// <summary>
        /// Copies the context for use by another node
        /// </summary>
        /// <param name="parent">New Parent to set</param>
        /// <returns>new <see cref="QueryContext"/> with all fields except <see cref="ParentObjectMetadata"/> the same</returns>
        public QueryContext CopyWithParent(DataSourceObjectMetadata parent)
        {
            QueryContext context = new QueryContext(this.Server, this.kustoUtils, this.ServiceProvider, this.smoWrapper)
            {
                database = this.Database,
                ParentObjectMetadata = parent,
                SqlServerType = this.SqlServerType,
                ValidFor = ValidFor
            };
            return context;
        }

        /// <summary>
        /// Indicates which platforms the server and database is valid for
        /// </summary>
        public ValidForFlag ValidFor
        {
            get
            {
                if(validFor == 0)
                {
                    validFor = ServerVersionHelper.GetValidForFlag(SqlServerType, Database);
                }
                return validFor;
            }
            set
            {
                validFor = value;
            }
        }

        private T GetObjectWithOpenedConnection<T>(T smoObj)
            where T : DataSourceObjectMetadata
        {
            if (smoObj != null)
            {
                EnsureConnectionOpen(smoObj);
            }
            return smoObj;
        }

        /// <summary>
        /// Ensures the server objects connection context is open. This is used by all child objects, and 
        /// the only way to easily access is via the server object. This should be called during access of
        /// any of the object properties
        /// </summary>
        public void EnsureConnectionOpen(DataSourceObjectMetadata smoObj)
        {
            if (!smoWrapper.IsConnectionOpen(smoObj))
            {
                // We have a closed server connection. Reopen this
                // Note: not currently catching connection exceptions. Expect this to bubble
                // up to calling methods and be logged there as this would be happening there in any case
                smoWrapper.OpenConnection(smoObj);
            }
        }
    }
}