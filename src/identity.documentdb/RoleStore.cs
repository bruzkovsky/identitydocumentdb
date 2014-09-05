﻿// MIT License Copyright 2014 (c) David Melendez. All rights reserved. See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using System.Net;
using System.Diagnostics;
using ElCamino.AspNet.Identity.DocumentDB.Model;
using ElCamino.AspNet.Identity.DocumentDB.Helpers;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Linq;
using Newtonsoft.Json;

namespace ElCamino.AspNet.Identity.DocumentDB
{
    public class RoleStore<TRole> : RoleStore<TRole, string, IdentityUserRole>, IQueryableRoleStore<TRole>, IQueryableRoleStore<TRole, string>, IRoleStore<TRole, string> where TRole : IdentityRole, new()
    {
        public RoleStore()
            : this(new IdentityCloudContext())
        {

        }

        public RoleStore(IdentityCloudContext context)
            : base(context) { }

        //Fixing code analysis issue CA1063
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }

    public class RoleStore<TRole, TKey, TUserRole> : IQueryableRoleStore<TRole, TKey>, IRoleStore<TRole, TKey>, IDisposable
        where TRole : IdentityRole<TKey,TUserRole>, new()
        where TUserRole : IdentityUserRole<TKey>, new()
    {
        private bool _disposed;
        private DocumentCollection _roleTable;

        public RoleStore(IdentityCloudContext<IdentityUser, IdentityRole, string, IdentityUserLogin, IdentityUserRole, IdentityUserClaim> context)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }
            this.Context = context;
            _roleTable = context.RoleDocumentCollection;
        }


        public async virtual Task CreateAsync(TRole role)
        {
            ThrowIfDisposed();
            if (role == null)
            {
                throw new ArgumentNullException("role");
            }

            ((IGenerateKeys)role).GenerateKeys();
            await new TaskFactory().StartNew(() =>
                {
                    var docTask = Context.Client.CreateDocumentAsync(Context.RoleDocumentCollection.DocumentsLink, role
                               , Context.RequestOptions, true);
                    docTask.Wait();
                    var doc = docTask.Result;
                    Context.SetSessionTokenIfEmpty(doc.SessionToken);
                    JsonConvert.PopulateObject(doc.Resource.ToString(), role);
                });
        }

        public async virtual Task DeleteAsync(TRole role)
        {
            ThrowIfDisposed();
            if (role == null)
            {
                throw new ArgumentNullException("role");
            }
            await new TaskFactory().StartNew(() =>
                {
                    var docTask =  Context.Client.DeleteDocumentAsync(role.SelfLink,
                            Context.RequestOptions);
                    docTask.Wait();
                    var doc = docTask.Result;
                    Context.SetSessionTokenIfEmpty(doc.SessionToken);
                });

        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                if (this.Context != null)
                {
                    this.Context.Dispose();
                }
                this._roleTable = null;
                this.Context = null;
                this._disposed = true;
            }
        }

        public Task<TRole> FindByIdAsync(TKey roleId)
        {
            this.ThrowIfDisposed();
            string key = roleId.ToString();
            return Task.FromResult<TRole>(FindById(key));
        }

        public Task<TRole> FindByNameAsync(string roleName)
        {
            this.ThrowIfDisposed();
            string key = KeyHelper.GenerateRowKeyIdentityRole(roleName);
            return Task.FromResult<TRole>(FindById(key));
        }

        private TRole FindById(string roleKeyString)
        {
            var doc = Context.Client.CreateDocumentQuery(Context.RoleDocumentCollection.DocumentsLink
                , new Microsoft.Azure.Documents.Client.FeedOptions() { SessionToken = Context.SessionToken, MaxItemCount = 1 })
                .Where(d => d.Id == roleKeyString)
                .Select(s => s)
                .ToList()
                .FirstOrDefault();

            TRole role = null;

            if (doc != null)
            {
                role = JsonConvert.DeserializeObject<TRole>(doc.ToString());
            }
            return role;
        }

        private void ThrowIfDisposed()
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException(base.GetType().Name);
            }
        }

        public async virtual Task UpdateAsync(TRole role)
        {
            ThrowIfDisposed();
            if (role == null)
            {
                throw new ArgumentNullException("role");
            }
        
            if (!KeyHelper.GenerateRowKeyIdentityRole(role.Name).Equals(role.Id.ToString(), StringComparison.Ordinal))
            {
                await new TaskFactory().StartNew(() =>
                    {
                        var delTask = Context.Client.DeleteDocumentAsync(role.SelfLink, Context.RequestOptions);
                        delTask.Wait();
                        Context.SetSessionTokenIfEmpty(delTask.Result.SessionToken);
                        CreateAsync(role).Wait();
                    });
            }

        }

        public IdentityCloudContext<IdentityUser, IdentityRole, string, IdentityUserLogin, IdentityUserRole, IdentityUserClaim> Context { get; private set; }

        public IQueryable<TRole> Roles
        {
            get
            {
                return Context.Client.CreateDocumentQuery<TRole>(Context.RoleDocumentCollection.DocumentsLink);
            }
        }

    }
}