// Copyright (C) 2026 Cory Joseph
// SPDX-License-Identifier: AGPL-3.0-or-later
// Query this helper's permission; never request or grant consent.
#import <AppKit/AppKit.h>
#import <Carbon/Carbon.h>
#include <stdbool.h>

int main(void) {
    @autoreleasepool {
        NSMutableArray *finders = [NSMutableArray array];
        for (NSRunningApplication *app in NSWorkspace.sharedWorkspace.runningApplications) {
            if ([app.bundleIdentifier isEqualToString:@"com.apple.finder"]) {
                [finders addObject:@{@"pid": @(app.processIdentifier),
                                     @"finishedLaunching": @(app.finishedLaunching),
                                     @"terminated": @(app.terminated),
                                     @"bundleURL": app.bundleURL.path ?: @""}];
            }
        }
        NSMutableDictionary *result = [@{
            @"scope": @"standalone helper identity; does not certify XCTest or osascript responsibility",
            @"askUserIfNeeded": @NO,
            @"finder": finders
        } mutableCopy];
        if (finders.count == 0) {
            result[@"queryStatus"] = @"not queried: Finder is not already running";
        } else {
            const char bundleID[] = "com.apple.finder";
            AEDesc target = {typeNull, NULL};
            OSStatus create = AECreateDesc(typeApplicationBundleID, bundleID, sizeof(bundleID)-1, &target);
            result[@"descriptorStatus"] = @(create);
            if (create == noErr) {
                OSStatus status = AEDeterminePermissionToAutomateTarget(&target, typeWildCard, typeWildCard, false);
                result[@"permissionStatus"] = @(status);
                AEDisposeDesc(&target);
            }
        }
        NSData *json = [NSJSONSerialization dataWithJSONObject:result options:NSJSONWritingPrettyPrinted error:nil];
        fwrite(json.bytes, 1, json.length, stdout);
        fputc('\n', stdout);
    }
    return 0;
}
