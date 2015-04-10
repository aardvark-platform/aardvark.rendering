namespace FingerTree2


type Affix<'a> = list<'a>

module AffixPatterns =
    let (|One|Two|Three|Four|) (a : Affix<'a>) =
        match a with
            | [a] -> One a
            | [a;b] -> Two(a,b)
            | [a;b;c] -> Three(a,b,c)
            | [a;b;c;d] -> Four(a,b,c,d)
            | _ -> failwith "affixes must have length 1 - 4"

    let One a = [a]
    let Two a b = [a;b]