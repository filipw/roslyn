// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.VisualStudio.Debugger.Evaluation;
using Microsoft.VisualStudio.Debugger.Evaluation.ClrCompilation;
using Microsoft.VisualStudio.Debugger.Metadata;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Microsoft.CodeAnalysis.ExpressionEvaluator
{
    /// <summary>
    /// Debugger type proxy expansion.
    /// </summary>
    /// <remarks>
    /// May include <see cref="System.Collections.IEnumerable"/> and
    /// <see cref="System.Collections.Generic.IEnumerable{T}"/> as special cases.
    /// (The proxy is not declared by an attribute, but is known to debugger.)
    /// </remarks>
    internal sealed class DebuggerTypeProxyExpansion : Expansion
    {
        internal static Expansion CreateExpansion(
            ResultProvider resultProvider,
            DkmInspectionContext inspectionContext,
            string name,
            TypeAndCustomInfo typeDeclaringMemberAndInfoOpt,
            TypeAndCustomInfo declaredTypeAndInfo,
            DkmClrValue value,
            bool childShouldParenthesize,
            string fullName,
            string childFullNamePrefix,
            ReadOnlyCollection<string> formatSpecifiers,
            DkmEvaluationResultFlags flags,
            string editableValue)
        {
            Debug.Assert((inspectionContext.EvaluationFlags & DkmEvaluationFlags.NoExpansion) == 0);

            // Note: The native EE uses the proxy type, even for
            // null instances, so statics on the proxy type are
            // displayed. That case is not supported currently.
            if (!value.IsNull)
            {
                var proxyType = value.Type.GetProxyType();
                if (proxyType != null)
                {
                    if ((inspectionContext.EvaluationFlags & DkmEvaluationFlags.ShowValueRaw) != 0)
                    {
                        var rawView = CreateRawView(resultProvider, inspectionContext, declaredTypeAndInfo, value);
                        Debug.Assert(rawView != null);
                        return rawView;
                    }

                    DkmClrValue proxyValue;
                    try
                    {
                        proxyValue = value.InstantiateProxyType(inspectionContext, proxyType);
                    }
                    catch
                    {
                        proxyValue = null;
                    }

                    if (proxyValue != null)
                    {
                        return new DebuggerTypeProxyExpansion(
                            inspectionContext,
                            proxyValue,
                            name,
                            typeDeclaringMemberAndInfoOpt,
                            declaredTypeAndInfo,
                            value,
                            childShouldParenthesize,
                            fullName,
                            childFullNamePrefix,
                            formatSpecifiers,
                            flags,
                            editableValue,
                            resultProvider.Formatter);
                    }
                }
            }

            return null;
        }

        private readonly EvalResultDataItem _proxyItem;
        private readonly string _name;
        private readonly TypeAndCustomInfo _typeDeclaringMemberAndInfoOpt;
        private readonly TypeAndCustomInfo _declaredTypeAndInfo;
        private readonly DkmClrValue _value;
        private readonly bool _childShouldParenthesize;
        private readonly string _fullName;
        private readonly string _childFullNamePrefix;
        private readonly ReadOnlyCollection<string> _formatSpecifiers;
        private readonly DkmEvaluationResultFlags _flags;
        private readonly string _editableValue;

        private DebuggerTypeProxyExpansion(
            DkmInspectionContext inspectionContext,
            DkmClrValue proxyValue,
            string name,
            TypeAndCustomInfo typeDeclaringMemberAndInfoOpt,
            TypeAndCustomInfo declaredTypeAndInfo,
            DkmClrValue value,
            bool childShouldParenthesize,
            string fullName,
            string childFullNamePrefix,
            ReadOnlyCollection<string> formatSpecifiers,
            DkmEvaluationResultFlags flags,
            string editableValue,
            Formatter formatter)
        {
            Debug.Assert(proxyValue != null);
            var proxyType = proxyValue.Type.GetLmrType();
            var proxyTypeAndInfo = new TypeAndCustomInfo(proxyType);
            var proxyMembers = MemberExpansion.CreateExpansion(
                inspectionContext,
                proxyTypeAndInfo,
                proxyValue,
                ExpansionFlags.IncludeBaseMembers,
                TypeHelpers.IsPublic,
                formatter);
            if (proxyMembers != null)
            {
                string proxyMemberFullNamePrefix = null;
                if (childFullNamePrefix != null)
                {
                    bool sawInvalidIdentifier;
                    var proxyTypeName = formatter.GetTypeName(proxyTypeAndInfo, escapeKeywordIdentifiers: true, sawInvalidIdentifier: out sawInvalidIdentifier);
                    if (!sawInvalidIdentifier)
                    {
                        proxyMemberFullNamePrefix = formatter.GetObjectCreationExpression(proxyTypeName, childFullNamePrefix);
                    }
                }
                _proxyItem = new EvalResultDataItem(
                    ExpansionKind.Default,
                    name: string.Empty,
                    typeDeclaringMemberAndInfo: default(TypeAndCustomInfo),
                    declaredTypeAndInfo: proxyTypeAndInfo,
                    parent: null,
                    value: proxyValue,
                    displayValue: null,
                    expansion: proxyMembers,
                    childShouldParenthesize: false,
                    fullName: null,
                    childFullNamePrefixOpt: proxyMemberFullNamePrefix,
                    formatSpecifiers: Formatter.NoFormatSpecifiers,
                    category: default(DkmEvaluationResultCategory),
                    flags: default(DkmEvaluationResultFlags),
                    editableValue: null,
                    inspectionContext: inspectionContext);
            }

            _name = name;
            _typeDeclaringMemberAndInfoOpt = typeDeclaringMemberAndInfoOpt;
            _declaredTypeAndInfo = declaredTypeAndInfo;
            _value = value;
            _childShouldParenthesize = childShouldParenthesize;
            _fullName = fullName;
            _childFullNamePrefix = childFullNamePrefix;
            _formatSpecifiers = formatSpecifiers;
            _flags = flags;
            _editableValue = editableValue;
        }

        internal override void GetRows(
            ResultProvider resultProvider,
            ArrayBuilder<EvalResultDataItem> rows,
            DkmInspectionContext inspectionContext,
            EvalResultDataItem parent,
            DkmClrValue value,
            int startIndex,
            int count,
            bool visitAll,
            ref int index)
        {
            if (_proxyItem != null)
            {
                _proxyItem.Expansion.GetRows(resultProvider, rows, inspectionContext, _proxyItem, _proxyItem.Value, startIndex, count, visitAll, ref index);
            }

            if (InRange(startIndex, count, index))
            {
                rows.Add(this.CreateRawViewRow(resultProvider, inspectionContext));
            }

            index++;
        }

        private EvalResultDataItem CreateRawViewRow(
            ResultProvider resultProvider,
            DkmInspectionContext inspectionContext)
        {
            return new EvalResultDataItem(
                ExpansionKind.RawView,
                _name,
                _typeDeclaringMemberAndInfoOpt,
                _declaredTypeAndInfo,
                parent: null,
                value: _value,
                displayValue: null,
                expansion: CreateRawView(resultProvider, inspectionContext, _declaredTypeAndInfo, _value),
                childShouldParenthesize: _childShouldParenthesize,
                fullName: _fullName,
                childFullNamePrefixOpt: _childFullNamePrefix,
                formatSpecifiers: Formatter.AddFormatSpecifier(_formatSpecifiers, "raw"),
                category: DkmEvaluationResultCategory.Data,
                flags: _flags | DkmEvaluationResultFlags.ReadOnly,
                editableValue: _editableValue,
                inspectionContext: inspectionContext);
        }

        private static Expansion CreateRawView(
            ResultProvider resultProvider,
            DkmInspectionContext inspectionContext,
            TypeAndCustomInfo declaredTypeAndInfo,
            DkmClrValue value)
        {
            return resultProvider.GetTypeExpansion(inspectionContext, declaredTypeAndInfo, value, ExpansionFlags.IncludeBaseMembers);
        }
    }
}
