#import "ChineseInput.h"
@implementation ChineseInput
- (instancetype)initWithBundle:(NSBundle *)bundle tricks:(NSArray *)tricks {
    if(!(self=[super init])) return nil;
    NSString *file=[NSString stringWithContentsOfURL:[bundle URLForResource:@"ChineseWords" withExtension:@"tsv"]
                                            encoding:NSUTF8StringEncoding error:NULL];
    NSMutableDictionary *byKey=[NSMutableDictionary dictionary];
    for(NSString *line in [file componentsSeparatedByCharactersInSet:NSCharacterSet.newlineCharacterSet]) {
        NSArray *fields=[line componentsSeparatedByString:@"\t"];
        if(fields.count>=4) byKey[fields[0]]=fields;
    }
    NSMutableArray *rules=[NSMutableArray array];
    for(NSArray *trick in tricks) {
        NSArray *rule=byKey[trick[0]];
        if(!rule) return nil;
        NSRange spellings=NSMakeRange(3,rule.count-3);
        for(NSUInteger j=0;j<2;j++) {
            if([rule indexOfObject:trick[j] inRange:spellings]!=NSNotFound)
                _pinyinAliases|=1u<<(rules.count*2+j);
        }
        [rules addObject:rule];
    }
    _rules=[rules copy];
    return self;
}
@end
