import re
import sys

VOWELS = frozenset('AEIOU')
CONSONANTS_COMMON = frozenset('TNSHRDLCMW')
CONSONANTS_RARE = frozenset('FGYPBKVJXZ')

def main():
    if len(sys.argv) < 2:
        print('Usage: filter_wordlist.py FULL_WORDLIST')
        sys.exit(1)
    pattern = re.compile('^[^a-zA-Z]*([a-zA-Z]{4,8})[^a-zA-Z]*$')
    in_file = open(sys.argv[1], 'r')
    out_file = open('words.txt', 'w')

    for line in in_file:
        match = pattern.match(line)
        if match is None:
            continue

        word = match.group(1).upper()
        letters = set(word)
        # Valid words have ≤2 vowels, ≤4 common cons, and ≤1 rare cons
        if len(VOWELS & letters) > 2:
            continue
        if len(CONSONANTS_COMMON & letters) > 4:
            continue
        if len(CONSONANTS_RARE & letters) > 1:
            continue

        print('word: {}'.format(word))
        out_file.write(word + '\n')

    in_file.close()
    out_file.close()

if __name__ == '__main__':
    main()
