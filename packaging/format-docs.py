#!/usr/bin/env python3
# Copyright 2007-2022 The OpenRA Developers (see AUTHORS)
# This file is part of OpenRA, which is free software. It is made
# available to you under the terms of the GNU General Public License
# as published by the Free Software Foundation, either version 3 of
# the License, or (at your option) any later version. For more
# information, see COPYING.

import io
import sys
import json
from collections import OrderedDict

def format_type_name(typeName):
    name = typeName
    if name.endswith("Info"):
        name = name[0:-4]

    return f'[`{name}`](#{name.lower()})'

def format_docs(version, collectionName, types):
    typesByNamespace = OrderedDict()
    for currentType in types:
        if currentType["Namespace"] in typesByNamespace:
            typesByNamespace[currentType["Namespace"]].append(currentType)
        else:
            typesByNamespace[currentType["Namespace"]] = [currentType]

    explanation = ""
    if collectionName == "TraitInfos":
        explanation = "all traits with their properties and their default values plus developer commentary"
    elif collectionName == "WeaponTypes":
        explanation = "a template for weapon definitions as well as its contained types (warheads and projectiles) with default values and developer commentary"
    elif collectionName == "SequenceTypes":
        explanation = "all sequence types with their properties and their default values plus developer commentary"

    print(f"This documentation is aimed at modders and has been automatically generated for version `{version}` of OpenRA. " +
				"Please do not edit it directly, but instead add new `[Desc(\"String\")]` tags to the source code.\n")

    print(f"Listed below are {explanation}:\n")

    for namespace in typesByNamespace:
        print(f'## {namespace}\n')

        for currentType in typesByNamespace[namespace]:
            print(f'### {currentType["Name"]}\n')

            if currentType["Description"]:
                print(f'#### {currentType["Description"]}\n')

            if "InheritedTypes" in currentType and currentType["InheritedTypes"]:
                print("Inherits from: " + ", ".join([format_type_name(x) for x in currentType["InheritedTypes"]]) + '.\n')

            if "RequiresTraits" in currentType and currentType["RequiresTraits"]:
                print("Requires trait(s): " + ", ".join([format_type_name(x) for x in currentType["RequiresTraits"]]) + '.\n')

            if len(currentType["Properties"]) > 0:
                hasAttributes = "OtherAttributes" in currentType["Properties"][0]
                print(f'| Property | Default Value | Type |{" Attributes |" if hasAttributes else ""} Description |')
                print(f'| -------- | ------------- | ---- |{" ---------- |" if hasAttributes else ""} ----------- |')

                for prop in currentType["Properties"]:
                    if "OtherAttributes" in prop:
                        attributesList = [f'{x["Name"]}{"(" + str(x["Value"][0]) + ")" if x["Value"] else ""}' for x in prop["OtherAttributes"] if x["Name"]]
                        if not attributesList:
                            attributes = ''
                        else:
                            attributes = ", ".join(attributesList)

                        print(f'| {prop["PropertyName"]} | {prop["DefaultValue"]} | {prop["UserFriendlyType"]} | {attributes} | {prop["Description"]} |')
                    else:
                        print(f'| {prop["PropertyName"]} | {prop["DefaultValue"]} | {prop["UserFriendlyType"]} | {prop["Description"]} |')

            print('\n#\n')

if __name__ == "__main__":
    input_stream = io.TextIOWrapper(sys.stdin.buffer, encoding='utf-8-sig')
    jsonInfo = json.load(input_stream)

    keys = list(jsonInfo)
    if len(keys) == 2 and keys[0] == 'Version':
        format_docs(jsonInfo[keys[0]], keys[1], jsonInfo[keys[1]])
