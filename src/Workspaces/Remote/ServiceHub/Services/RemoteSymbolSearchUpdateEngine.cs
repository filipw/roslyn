﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.SymbolSearch;

namespace Microsoft.CodeAnalysis.Remote
{
    internal partial class RemoteSymbolSearchUpdateEngine : ServiceHubServiceBase, IRemoteSymbolSearchUpdateEngine
    {
        private readonly SymbolSearchUpdateEngine _updateEngine;

        public RemoteSymbolSearchUpdateEngine(Stream stream, IServiceProvider serviceProvider)
            : base(serviceProvider, stream)
        {
            _updateEngine = new SymbolSearchUpdateEngine(
                new LogService(this), new ProgressService(this),
                updateCancellationToken: this.CancellationToken);

            Rpc.StartListening();
        }

        public Task UpdateContinuouslyAsync(string sourceName, string localSettingsDirectory)
        {
            return _updateEngine.UpdateContinuouslyAsync(sourceName, localSettingsDirectory);
        }

        public async Task<SerializablePackageWithTypeResult[]> FindPackagesWithTypeAsync(string source, string name, int arity)
        {
            var results = await _updateEngine.FindPackagesWithTypeAsync(
                source, name, arity).ConfigureAwait(false);
            var serializedResults = results.Select(SerializablePackageWithTypeResult.Dehydrate).ToArray();
            return serializedResults;
        }

        public async Task<SerializablePackageWithAssemblyResult[]> FindPackagesWithAssemblyAsync(string source, string assemblyName)
        {
            var results = await _updateEngine.FindPackagesWithAssemblyAsync(
                source, assemblyName).ConfigureAwait(false);
            var serializedResults = results.Select(SerializablePackageWithAssemblyResult.Dehydrate).ToArray();
            return serializedResults;
        }

        public async Task<SerializableReferenceAssemblyWithTypeResult[]> FindReferenceAssembliesWithTypeAsync(string name, int arity)
        {
            var results = await _updateEngine.FindReferenceAssembliesWithTypeAsync(
                name, arity).ConfigureAwait(false);
            var serializedResults = results.Select(SerializableReferenceAssemblyWithTypeResult.Dehydrate).ToArray();
            return serializedResults;
        }
    }
}